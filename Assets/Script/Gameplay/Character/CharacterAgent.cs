using System;
using UnityEngine;
using Wargency.Core;

namespace Wargency.Gameplay
{
    // Script kiểm soát vòng đời làm việc của nhân vật
    // - Giữ state: Idle/Moving/Working/Resting (M3 bỏ Moving/path)
    // - Tick tiêu hao/hồi phục theo difficulty
    // - Đóng góp tiến độ task qua TaskManager dựa trên Productivity01() bên character stat
    [RequireComponent(typeof(CharacterStats))] //bắt buộc phải có component kia nha, xài để tự gắn lun cho chắc
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent] //ko cho gắn 2 lần, xài thử
    public class CharacterAgent : MonoBehaviour
    {
        //Trạng thái nhân vật nhe
        public enum AgentState { Idle, Moving, Working, Resting }

        [Header("Refs")]
        [SerializeField] private CharacterDefinition definition;  // dữ liệu nhân vật (avatar/body/moveSpeed/base stats)
        [SerializeField] private CharacterStats stats;            // Runtime coi stats (Energy/Stress)
        [SerializeField] private SpriteRenderer spriteRenderer;   // Hiển thị body

        [SerializeField, Tooltip("Optional: null thì FindAnyObjectByType")]
        private TaskManager taskManager;                          // Để thêm bớt cộng trừ tiến độ task

        [Header("Difficulty setting")]
        [SerializeField, Tooltip("Kéo WaveManager (implement IDifficultyProvider)")]
        private UnityEngine.Object difficultyProviderObj;         // Để tham chiều vào
        private IDifficultyProvider difficultyProvider;           // Interface: TaskSpeedMultiplier, EnergyDrainPerSec, StressGainPerSec

        [Header("Tỷ lệ Working / Resting (fallback nếu chưa có WaveManager)")]
        [Min(0f)][SerializeField] private float baseEnergyDrainPerSecWorking = 6f; //mỗi giây tốn đây sức
        [Min(0f)][SerializeField] private float baseStressGainPerSecWorking = 3f;
        [Min(0f)][SerializeField] private float restEnergyPerSec = 12f; // mỗi giây nghỉ tăng nhiêu đây
        [Min(0f)][SerializeField] private float restStressRecoverPerSec = 8f;

        // chỉ lúc chạy chứ ko có chỉnh inpsector
        private AgentState state = AgentState.Idle;
        private TaskInstance currentTask;

        // Sự kiện public để UI/Hệ thống khác nắm
        public event Action<CharacterAgent, TaskInstance> OnAssigned; // Nhân vật nào nhận task nào nè
        public event Action<CharacterAgent, TaskInstance> OnReleased; // Khi xong task
        public event Action<CharacterAgent> OnStatsChanged;           // Dính dáng được gọi forward từ CharacterStats.StatsChanged

        //Public API = getters
        public CharacterDefinition Definition => definition;
        public TaskInstance CurrentTask => currentTask;
        public AgentState State => state;
        public CharacterRole Role { get; }

        private void Awake()
        {
            // Ko tìm thấy component thì tự ref vô
            if (!stats) stats = GetComponent<CharacterStats>();
            if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (!taskManager) taskManager = FindAnyObjectByType<TaskManager>();

            // Event liên quan đế agent-level event (HUD sẽ subscribe event này sau ở M4)
            stats.StatsChanged += (_, __) => OnStatsChanged?.Invoke(this);

            // Resolve difficulty provider qua object kéo vào Inspector, kiểm tra coi có inteface implement bên object không rồi, nếu có ép thành kiểu IDificilltyProvider và gán vào p
            if (difficultyProviderObj is IDifficultyProvider p) difficultyProvider = p;

            // Set sprite body từ Definition (nếu có)
            if (definition && definition.Body) spriteRenderer.sprite = definition.Body;

            // khởi tạo stats từ Definition của character
            if (definition) stats.InitFrom(definition);

            // Mặc định rảnh, chưa có task
            state = AgentState.Idle;
            currentTask = null;
        }

        private void Update()
        {
            // Sắp xếp sprite theo Y để đúng thứ tự hiển thị isometric/topdown. Viết thêm lần nữa cho chắc thôi
            if (spriteRenderer)
                spriteRenderer.sortingOrder = IsometricHelper.OrderFromY(transform.position.y);

            TickState(Time.deltaTime);
        }

        private void TickState(float dt) // kiểm tra state để gọi hàm thực hiện các state specific khác
        {
            switch (state) //sau này thêm chế độ khác thì thêm case
            {
                case AgentState.Working: TickWorking(dt); break;
                case AgentState.Resting: TickResting(dt); break;
                    // Idle/Moving: M3 chưa làm gì nhé
            }
        }

        // đang làm việc: đóng góp tiến độ + tiêu hao
        private void TickWorking(float deltatime)
        {
            // Đọc modifier từ difficulty (nếu không có, dùng base)
            float speedMultiplier = difficultyProvider?.TaskSpeedMultiplier ?? 1f;
            float drain = difficultyProvider?.EnergyDrainPerSec ?? baseEnergyDrainPerSecWorking;
            float stressGain = difficultyProvider?.StressGainPerSec ?? baseStressGainPerSecWorking;

            // Đóng góp tiến độ task theo Productivity (0..1), nhân multiplier theo thời gian
            if (currentTask != null)
            {
                float productivity = stats.Productivity01();
                float delta01 = productivity * speedMultiplier * deltatime;
                taskManager?.ContributeProgress(currentTask, delta01);
            }

            // Tiêu hao/hấp thụ stress (đi qua ApplyDelta để clamp + bắn event)
            int drainEnergy = Mathf.FloorToInt(-drain * deltatime); //-drain để chắc chắn giảm energy, làm tròn xuống thành số nguyên
            int drainStress = Mathf.FloorToInt(+stressGain * deltatime); //+stress để tăng stress trong frame này
            stats.ApplyDelta(drainEnergy, drainStress);

            // Điều kiện kiệt sức hết năng luộng or quá stress -> về Resting
            if (stats.Energy <= 0 || stats.Stress >= stats.MaxStress)
            {
                ReleaseTask();          // hạ state Working về rest
                state = AgentState.Resting;
            }
        }

        //Hàm nghỉ ngơi
        private void TickResting(float dt)
        {
            int dEnergy = Mathf.CeilToInt(restEnergyPerSec * dt);
            int dStress = -Mathf.CeilToInt(restStressRecoverPerSec * dt);
            stats.ApplyDelta(dEnergy, dStress);

            // Hồi “đủ tốt” -> về Idle (ngưỡng 90%/10% là con số dễ hiểu ở M3)
            if (stats.Energy >= stats.MaxEnergy * 0.9f && stats.Stress <= stats.MaxStress * 0.1f)
                state = AgentState.Idle; // nghỉ đủ rồi cho về lại idle
        }

        // Thiết lập nhanh sau khi dùng Instantiate tạo prefab
        public void SetupCharacter(CharacterDefinition def, IDifficultyProvider difficulty = null, TaskManager tm = null)
        {

            var resolved = definition != null ? definition : def;
            if (resolved == null)
            {
                Debug.LogError($"[Agent] Ko tìm thấy CharacterDefinition ở {name}. Check lại prefab hoặc để SetupCharacter");
                return;
            }
            definition = resolved;
            if (definition && spriteRenderer && definition.Body)
                spriteRenderer.sprite = definition.Body;

            stats.InitFrom(definition);
            if (difficulty != null) difficultyProvider = difficulty;
            if (tm != null) taskManager = tm;

            state = AgentState.Idle;
            currentTask = null;
        }

        //Cho phép đổi difficulty provider runtime (vd khi Wave đổi)
        public void SetDifficultyProvider(IDifficultyProvider provider) => difficultyProvider = provider;

        // Nhận task: chỉ nhận khi không ở Working (tránh đè)
        public void AssignTask(TaskInstance task)
        {
            if (task == null) return;
            if (state == AgentState.Working) return;

            currentTask = task;
            state = AgentState.Working;
            OnAssigned?.Invoke(this, task);
        }

        // Kết thúc giải phóng khỏi task: check task hiện tại, phát sự kiện, đặt state của nhân vật theo stat
        public void ReleaseTask()
        {
            if (currentTask != null)
            {
                var t = currentTask;
                currentTask = null;
                OnReleased?.Invoke(this, t);
            }

            // nếu yếu/kiệt -> Resting, ngược lại Idle
            state = (stats.Energy <= stats.MaxEnergy * 0.2f || stats.Stress >= stats.MaxStress * 0.8f)
                ? AgentState.Resting
                : AgentState.Idle;
        }

        // hệ thống ngoài kết thúc/hủy task (TaskManager/WaveManager gọi)
        public void OnExternalTaskTerminated(TaskInstance task)
        {
            if (task != null && task == currentTask)
                ReleaseTask();
        }
    }
}