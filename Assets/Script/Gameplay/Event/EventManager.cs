using System;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Wargency.Gameplay
{
    // file này quản lý random event trong lúc chơi
    // lọc theo wave nếu có WaveManager
    // có 2 flow => Instant áp ngay, Choice chờ người chơi chọn rồi mới xử lý
    // bắn event cho UI => OnEventTrigger để mở popup, OnEventText để show text, OnChoiceEvent để hiển thị lựa chọn
    // đừng để 2 EventManager cùng lúc trong scene nha

    public class EventManager : MonoBehaviour
    {
        // danh sách event nguồn => kéo vào trong inspector
        [Header("Config")]
        public EventDefinition[] availableEvents;

        // tham chiếu sang controller khác => áp budget/score và biết wave
        [Header("Refs")]
        public GameLoopController gameLoopController; // áp budget/score
        [Tooltip("Để lọc event theo wave (unlockAtWave). Không bắt buộc, nếu trống thì không lọc.")]
        public WaveManager waveManager;               // dùng GetCurrentWaveIndex()+1

        // các event để UI lắng nghe => mở popup, text feed, và choice
        // OnEventResolved để đếm objective ResolveEvents
        public event Action<EventDefinition> OnEventTrigger; // popup tiêu đề/mô tả
        public event Action<string> OnEventText;             // hiển thị “+Energy/-Stress ...”
        public event Action<ChoiceEventData> OnChoiceEvent;  // hiển thị 2 lựa chọn
        public event Action OnEventResolved;                 // báo đã xử lý xong 1 event

        // trạng thái chạy bên trong
        float timer;
        float min;
        float max;
        bool isWaitingChoice = false;

        // khởi động => tính min/max interval, tìm ref nếu để trống, reset timer
        void Start()
        {
            if (availableEvents == null || availableEvents.Length == 0)
            {
                Debug.LogWarning("[EventManager] Không có Event nào.");
                enabled = false;
                return;
            }

            // lấy min/max từ list event để làm khoảng random timer
            min = Mathf.Max(0.1f, availableEvents.Min(e => e != null ? e.minIntervalSec : 999f));
            max = Mathf.Max(min + 0.1f, availableEvents.Max(e => e != null ? e.maxIntervalSec : 0.1f));

            if (!gameLoopController) gameLoopController = FindFirstObjectByType<GameLoopController>();
            if (!waveManager) waveManager = FindFirstObjectByType<WaveManager>();

            ResetTimer();
        }

        // đếm ngược timer => hết giờ thì bắn event mới
        // nếu đang chờ choice thì không spawn thêm
        void Update()
        {
            if (isWaitingChoice) return;

            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                TriggerRandomEvent();
                ResetTimer();
            }
        }

        // chọn 1 event đang unlock theo wave và bắn ra cho UI
        // nếu là Instant => xử lý ngay, nếu là Choice => bật UI chờ chọn
        public void TriggerRandomEvent()
        {
            var pool = availableEvents?.Where(e => e != null).ToArray();
            if (pool == null || pool.Length == 0) return;

            int currentWaveOneBased = waveManager != null ? waveManager.GetCurrentWaveIndex() + 1 : 1;

            var unlockedPool = pool
                .Where(e => Mathf.Max(1, e.unlockAtWave) <= currentWaveOneBased)
                .ToArray();

            if (unlockedPool.Length == 0) return;

            var chosen = unlockedPool[Random.Range(0, unlockedPool.Length)];
            if (chosen == null) return;

            OnEventTrigger?.Invoke(chosen);

            if (chosen.kind == EventDefinition.EventKind.Instant)
            {
                ApplyInstant(chosen);
            }
            else
            {
                isWaitingChoice = true;
                var payload = ChoiceEventData.From(chosen);
                OnChoiceEvent?.Invoke(payload);
            }
        }

        // flow Instant => áp budget/score ngay, đẩy text UI, báo đã resolve
        void ApplyInstant(EventDefinition e)
        {
            if (gameLoopController != null)
            {
                if (e.budgetChange != 0) gameLoopController.AddBudget(e.budgetChange);
                if (e.scoreChange != 0) gameLoopController.AddScore(e.scoreChange);
            }
            else Debug.LogWarning("[EventManager] gameLoopController null.");

            var text = BuildTeamText(e.teamEnergyTextDelta, e.teamStressTextDelta);
            if (!string.IsNullOrEmpty(text))
            {
                OnEventText?.Invoke(text);
                var feed = FindFirstObjectByType<Wargency.UI.UILiveAlertsFeed>();
                if (feed != null) feed.Push(text);
            }

            OnEventResolved?.Invoke(); // đếm xong 1 event
        }

        // flow Choice với event gốc => người chơi chọn A/B xong thì xử lý tác động, báo resolve và chạy tiếp
        public void ApplyChoice(EventDefinition root, bool chooseA)
        {
            if (root == null || root.kind != EventDefinition.EventKind.Choice)
            {
                Debug.LogWarning("[EventManager] ApplyChoice: root invalid.");
                return;
            }

            var opt = chooseA ? root.optionA : root.optionB;
            if (opt == null)
            {
                Debug.LogWarning("[EventManager] ApplyChoice: option null.");
                return;
            }

            if (gameLoopController != null)
            {
                if (opt.budgetChange != 0) gameLoopController.AddBudget(opt.budgetChange);
                if (opt.scoreChange != 0) gameLoopController.AddScore(opt.scoreChange);
            }
            else Debug.LogWarning("[EventManager] gameLoopController null.");

            var text = BuildTeamText(opt.teamEnergyTextDelta, opt.teamStressTextDelta);
            if (!string.IsNullOrEmpty(text))
            {
                OnEventText?.Invoke(text);
                var feed = FindFirstObjectByType<Wargency.UI.UILiveAlertsFeed>();
                if (feed != null) feed.Push(text);
            }

            OnEventResolved?.Invoke(); // đếm xong 1 event

            isWaitingChoice = false;
            ResetTimer();
        }

        // flow Choice overload => đưa thẳng 1 option vào để áp
        public void ApplyChoice(EventDefinition.ChoiceOption option)
        {
            if (option == null)
            {
                Debug.LogWarning("[EventManager] ApplyChoice(option): null.");
                return;
            }

            if (gameLoopController != null)
            {
                if (option.budgetChange != 0) gameLoopController.AddBudget(option.budgetChange);
                if (option.scoreChange != 0) gameLoopController.AddScore(option.scoreChange);
            }

            var text = BuildTeamText(option.teamEnergyTextDelta, option.teamStressTextDelta);
            if (!string.IsNullOrEmpty(text))
            {
                OnEventText?.Invoke(text);
                var feed = FindFirstObjectByType<Wargency.UI.UILiveAlertsFeed>();
                if (feed != null) feed.Push(text);
            }

            OnEventResolved?.Invoke(); // đếm xong 1 event

            isWaitingChoice = false;
            ResetTimer();
        }

        // random lại khoảng thời gian cho lần event kế tiếp
        void ResetTimer() => timer = Random.Range(min, max);

        // ghép text Energy/Stress để show UI feed => trả chuỗi rỗng nếu không có gì
        static string BuildTeamText(int energyDelta, int stressDelta)
        {
            string e = energyDelta == 0 ? "" : (energyDelta > 0 ? $"+{energyDelta} Energy team" : $"{energyDelta} Energy team");
            string s = stressDelta == 0 ? "" : (stressDelta > 0 ? $"+{stressDelta} Stress team" : $"{stressDelta} Stress team");

            if (string.IsNullOrEmpty(e) && string.IsNullOrEmpty(s)) return "";
            if (!string.IsNullOrEmpty(e) && !string.IsNullOrEmpty(s)) return $"{e} • {s}";
            return string.IsNullOrEmpty(e) ? s : e;
        }

        // gói dữ liệu gọn cho UI Choice => gom text và giữ trỏ về event gốc
        [Serializable]
        public struct ChoiceEventData
        {
            public string id;
            public string title;
            public string description;
            public string optionATitle;
            public string optionADesc;
            public string optionBTitle;
            public string optionBDesc;
            public EventDefinition root;

            public static ChoiceEventData From(EventDefinition def)
            {
                return new ChoiceEventData
                {
                    id = def.id,
                    title = def.title,
                    description = def.description,
                    optionATitle = def.optionA != null ? def.optionA.optionTitle : "A",
                    optionADesc = def.optionA != null ? def.optionA.optionDescription : "",
                    optionBTitle = def.optionB != null ? def.optionB.optionTitle : "B",
                    optionBDesc = def.optionB != null ? def.optionB.optionDescription : "",
                    root = def
                };
            }
        }
    }

    // lưu ý
    // - component này chạy ở runtime => không lưu file
    // - nếu muốn tắt event tạm thời thì disable component
    // - đổi wave xong mà muốn làm mới nhịp event => gọi ResetTimer
}
