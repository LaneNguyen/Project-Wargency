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

        [Header("UI")]
        [SerializeField, Tooltip("Portrait/icon for UI panels. Fallback to Definition.Body if null")] private Sprite portraitIcon;

        [SerializeField, Tooltip("Optional: null thì FindAnyObjectByType")]
        private TaskManager taskManager;                          // Để thêm bớt cộng trừ tiến độ task

        [Header("Difficulty setting")]
        [SerializeField, Tooltip("Kéo WaveManager (implement IDifficultyProvider)")]
        private UnityEngine.Object difficultyProviderObj;         // Để tham chiều vào
        private IDifficultyProvider difficultyProvider;           // Interface: TaskSpeedMultiplier, EnergyDrainPerSec, StressGainPerSec

        [Header("Tỷ lệ Working / Resting (fallback nếu chưa có WaveManager)")]
        [Min(0f)][SerializeField] private float baseEnergyDrainPerSecWorking = 6f; //mỗi giây tốn đây sức
        [Min(0f)][SerializeField] private float baseStressGainPerSecWorking = 3f;
        [Min(0f)][SerializeField] private float restEnergyPerSec = 2f; // mỗi giây nghỉ tăng nhiêu đây
        [Min(0f)][SerializeField] private float restStressRecoverPerSec = 2f;

        private float _restEnergyAcc;
        private float _restStressAcc;

        // UPDATE 2408: Tùy chọn tự động đăng ký với TaskManager
        [Header("TaskManager Auto-Register")]
        [SerializeField, Tooltip("Tự động Register/Unregister với TaskManager khi Agent bật/tắt.")]
        private bool autoRegisterToTaskManager = true; // UPDATE 2408

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
        public string DisplayName => Definition != null ? Definition.DisplayName : name;
        public CharacterRole Role => Definition != null ? Definition.Role : CharacterRole.Planner;
        public Sprite IconSprite => portraitIcon != null ? portraitIcon : (Definition != null && Definition.Body ? Definition.Body : null);
        public TaskInstance CurrentAssignment { get; private set; } // dành cho kiểm soát assign

        // fix: cờ nhỏ để tránh đăng ký TaskManager 2 lần dẫn tới KPI đếm đôi
        private bool _isRegisteredToTM = false; // sẽ set true khi gọi RegisterAgent thành công


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


        // UPDATE 2408: Auto-register với TaskManager khi bật
        private void OnEnable() // UPDATE 2408
        {
            var tm = FindObjectOfType<TaskManager>();
            if (tm) tm.RegisterAgent(this);

            if (!taskManager) taskManager = FindAnyObjectByType<TaskManager>();
            if (autoRegisterToTaskManager && taskManager != null)
            {
                if (!_isRegisteredToTM)
                {
                    taskManager.RegisterAgent(this);
                    _isRegisteredToTM = true; // fix: đánh dấu đã đăng ký để không bị đếm đôi
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[Agent] OnEnable → RegisterAgent: {name} (Role: {Role}) TM id={taskManager.GetInstanceID()}");
#endif
                }
            }
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

        // UPDATE 2408: Unregister khi tắt
        private void OnDisable() // UPDATE 2408
        {
            var tm = FindObjectOfType<TaskManager>();
            if (tm) tm.UnregisterAgent(this);
            if (autoRegisterToTaskManager && taskManager != null)
            {
                if (_isRegisteredToTM)
                {
                    taskManager.UnregisterAgent(this);
                    _isRegisteredToTM = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[Agent] OnDisable → UnregisterAgent: {name}");
#endif
                }
            }
        }

        // đang làm việc: đóng góp tiến độ + tiêu hao
        // Thêm 2 biến private ở đầu class (ngay sau currentTask):
        // Tích lũy phần lẻ để tránh FloorToInt về 0 liên tục
        private float _energyAcc;  // âm dần (tiêu hao)
        private float _stressAcc;  // dương dần (tăng stress)

        // 

        private void TickWorking(float deltatime)
        {
            // 1) Lấy base từ difficulty hoặc fallback
            float speedMultiplier = difficultyProvider?.TaskSpeedMultiplier ?? 1f;
            float drain = difficultyProvider?.EnergyDrainPerSec ?? baseEnergyDrainPerSecWorking;
            float stressGain = difficultyProvider?.StressGainPerSec ?? baseStressGainPerSecWorking;

            // 2) Bổ sung modifier theo task (per-second)
            if (currentTask != null)
            {
                // energyCost → tiêu hao thêm mỗi giây
                int extraEnergyPerSec = currentTask.Definition != null ? currentTask.Definition.energyCost : 0;
                if (extraEnergyPerSec > 0) drain += extraEnergyPerSec;

                // stressImpact → đã được nhồi vào task.stressCost khi Spawn
                int extraStressPerSec = currentTask.stressCost >= 0 ? currentTask.stressCost : 0;
                if (extraStressPerSec > 0) stressGain += extraStressPerSec;
            }

            // 3) Đóng góp tiến độ (như cũ)
            if (currentTask != null)
            {
                float productivity = stats.Productivity01();
                float delta01 = productivity * speedMultiplier * deltatime;
                taskManager?.ContributeProgress(currentTask, delta01);
            }

            // 4) TÍCH LŨY PHẦN LẺ → chỉ khi đủ 1 điểm mới trừ/cộng vào int
            _energyAcc += -drain * deltatime;      // âm dần
            _stressAcc += +stressGain * deltatime; // dương dần

            int dEnergy = 0;
            int dStress = 0;

            // energy: khi phần lẻ <= -1 thì trừ bấy nhiêu và giữ lại phần dư
            if (_energyAcc <= -1f)
            {
                dEnergy = Mathf.FloorToInt(_energyAcc); // ví dụ -1, -2, ...
                _energyAcc -= dEnergy;                  // trừ đi phần đã dùng (dEnergy âm nên -(-n) = +n)
            }

            // stress: khi phần lẻ >= +1 thì cộng bấy nhiêu và giữ lại phần dư
            if (_stressAcc >= +1f)
            {
                dStress = Mathf.FloorToInt(_stressAcc); // 1,2,3,...
                _stressAcc -= dStress;
            }

            if (dEnergy != 0 || dStress != 0)
                stats.ApplyDelta(dEnergy, dStress);

            // 5) Điều kiện kiệt sức/quá tải → Resting
            if (stats.Energy <= 0 || stats.Stress >= stats.MaxStress)
            {
                ReleaseTask();
                state = AgentState.Resting;
            }
        }


        //Hàm nghỉ ngơi
        private void TickResting(float dt)
        {
            _restEnergyAcc += restEnergyPerSec * dt;
            _restStressAcc += restStressRecoverPerSec * dt;

            int dEnergy = 0;
            int dStress = 0;

            if (_restEnergyAcc >= 1f)
            {
                dEnergy = Mathf.FloorToInt(_restEnergyAcc);
                _restEnergyAcc -= dEnergy;
            }

            if (_restStressAcc >= 1f)
            {
                dStress = -Mathf.FloorToInt(_restStressAcc); // giảm stress
                _restStressAcc -= -dStress;
            }

            if (dEnergy != 0 || dStress != 0)
                stats.ApplyDelta(dEnergy, dStress);

            if (stats.Energy >= stats.MaxEnergy * 0.9f && stats.Stress <= stats.MaxStress * 0.1f)
                state = AgentState.Idle;
        }

        // Thiết lập nhanh sau khi dùng Instantiate tạo prefab
        public void SetupCharacter(CharacterDefinition def, IDifficultyProvider difficulty = null, TaskManager tm = null)
        {
            DefinitionAssign(def); // tách nhỏ cho gọn
            if (difficulty != null) difficultyProvider = difficulty;
            if (tm != null) taskManager = tm;

            // UPDATE 2408: Đảm bảo đã đăng ký vào TM ngay sau khi setup (idempotent)
            if (autoRegisterToTaskManager)
            {
                if (!taskManager) taskManager = FindAnyObjectByType<TaskManager>();
                if (taskManager != null && !_isRegisteredToTM)
                {
                    taskManager.RegisterAgent(this); // fix: chỉ đăng ký khi chưa đăng ký để khỏi đếm gấp đôi
                    _isRegisteredToTM = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[Agent] SetupCharacter → RegisterAgent: {name} (Role: {Role}) TM id={taskManager.GetInstanceID()}");
#endif
                }
            }

            state = AgentState.Idle;
            currentTask = null;
        }

        // UPDATE 2408: gom logic gán definition + sprite + init stat
        private void DefinitionAssign(CharacterDefinition def) // UPDATE 2408
        {
            var resolved = definition != null ? definition : def;
            if (resolved == null)
            {
                Debug.LogError($"[Agent] Không tìm thấy CharacterDefinition ở {name}. Kiểm tra prefab/SetupCharacter.");
                return;
            }

            definition = resolved;

            if (definition && spriteRenderer && definition.Body)
                spriteRenderer.sprite = definition.Body;

            if (stats != null)
                stats.InitFrom(definition);
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

        // internal để chỉ TaskInstance gọi
        internal void __SetAssignment(TaskInstance task)
        {
            CurrentAssignment = task;
        }
    }
}