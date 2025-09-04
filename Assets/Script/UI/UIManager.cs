using System;
using UnityEngine;
using Wargency.Gameplay;

namespace Wargency.UI
{
    // quản lý UI tổng quát cho game cho gọn
    // có pause resume nhẹ nhàng và dev hotkeys để thử vòng lặp
    // gom bốn mảng chức năng chính: Leadership, HumanResource, Deadline, Event
    // mở khóa theo wave bằng codeId, kèm intro panel tạm dừng game nếu cần
    // UI => Manager => Gameplay nói chuyện chủ yếu qua GameLoopController

    public class UIManager : MonoBehaviour
    {
        public static UIManager instance { get; private set; }

        [Header("Pause")]
        [SerializeField] private GameObject pausePanel;

        [Header("Developer Hotkeys")]
        [SerializeField] private bool enableDevHotkey = true;
        [SerializeField] private KeyCode keyAddBudget = KeyCode.B;
        [SerializeField] private KeyCode keySpendBudget = KeyCode.N;
        [SerializeField] private KeyCode keyWaveDecrease = KeyCode.LeftBracket;
        [SerializeField] private KeyCode keyWaveIncrease = KeyCode.RightBracket;
        [SerializeField] private KeyCode keyPauseToggle = KeyCode.Escape;

        [Header("Game Features")]
        [SerializeField] private FeatureEntry leadership;     // panel chọn skill leader
        [SerializeField] private FeatureEntry humanResource;  // panel tuyển dụng nhân sự
        [SerializeField] private FeatureEntry deadline;       // bảng deadline hay cảnh báo
        [SerializeField] private FeatureEntry eventFeature;   // panel sự kiện trong game

        private bool isPaused = false;
        private int lastKnownWave = int.MinValue;

        private void Awake()
        {
            if (instance == null) instance = this;
            else { Destroy(gameObject); return; }
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (pausePanel) pausePanel.SetActive(false);

            // UIManager không giữ tham chiếu UIBudget/UIScore/UIWave nữa
            // các stub đó tự nghe event từ GameLoopController

            // đồng bộ mở khóa ban đầu
            SyncFeatureLocks(forceIntroCheck: true);

            if (GameLoopController.Instance == null)
                Debug.LogWarning("[UIManager] GameLoopController.Instance chưa sẵn sàng");
        }

        private void Update()
        {
            HandleHotkeys();
            PollWaveChangeForUnlock();
        }

        // ===== hotkeys cho QA/dev =====
        private void HandleHotkeys()
        {
            if (!enableDevHotkey) return;
            var loop = GameLoopController.Instance;
            if (loop == null) return;

            if (Input.GetKeyDown(keyAddBudget))
            {
                loop.AddBudget(100);
                Debug.Log("[UIManager] +100 Budget (dev)");
            }

            if (Input.GetKeyDown(keySpendBudget))
            {
                bool ok = loop.TrySpendBudget(50);
                Debug.Log(ok ? "[UIManager] -50 Budget (dev)" : "[UIManager] Không đủ tiền");
            }

            if (Input.GetKeyDown(keyWaveDecrease))
            {
                loop.SetWave(loop.Wave - 1);
                Debug.Log($"[UIManager] Wave -> {loop.Wave} (dev)");
            }

            if (Input.GetKeyDown(keyWaveIncrease))
            {
                loop.SetWave(loop.Wave + 1);
                Debug.Log($"[UIManager] Wave -> {loop.Wave} (dev)");
            }

            if (Input.GetKeyDown(keyPauseToggle))
            {
                TogglePause();
            }
        }

        // ===== pause đơn giản bằng timeScale =====
        public void TogglePause()
        {
            isPaused = !isPaused;
            Time.timeScale = isPaused ? 0f : 1f;
            if (pausePanel) pausePanel.SetActive(isPaused);
            Debug.Log(isPaused ? "UI Manager: PAUSED" : "UI Manager: Resumed");
        }

        public void SetPause(bool pause)
        {
            if (isPaused == pause) return;
            isPaused = pause;
            Time.timeScale = isPaused ? 0f : 1f;
            if (pausePanel) pausePanel.SetActive(isPaused);
            Debug.Log(isPaused ? "UI Manager: PAUSED" : "UI Manager: Resumed");
        }

        // ===== chỉ theo dõi wave để mở khóa tính năng =====
        private void PollWaveChangeForUnlock()
        {
            var loop = GameLoopController.Instance;
            if (loop == null) return;

            if (lastKnownWave != loop.Wave)
            {
                lastKnownWave = loop.Wave;
                SyncFeatureLocks(forceIntroCheck: true);
            }
        }

        private void SyncFeatureLocks(bool forceIntroCheck)
        {
            var wave = GameLoopController.Instance != null ? GameLoopController.Instance.Wave : 0;
            ApplyFeatureLock(leadership, wave, forceIntroCheck);
            ApplyFeatureLock(humanResource, wave, forceIntroCheck);
            ApplyFeatureLock(deadline, wave, forceIntroCheck);
            ApplyFeatureLock(eventFeature, wave, forceIntroCheck);
        }

        private void ApplyFeatureLock(FeatureEntry entry, int wave, bool forceIntroCheck)
        {
            if (entry == null) return;

            bool unlocked = wave >= entry.unlockWave;
            if (entry.root) entry.root.SetActive(unlocked);

            // khi vừa mở khóa và muốn hiện hướng dẫn thì hiển thị một lần
            if (unlocked && (forceIntroCheck || !entry.wasIntroChecked))
            {
                entry.wasIntroChecked = true;
                if (entry.showIntroOnUnlock)
                    ShowFeatureIntro(entry);
            }
        }

        // ===== intro panel cho chức năng =====
        public void ShowFeatureIntro(FeatureEntry entry)
        {
            if (entry == null || entry.introPanel == null) return;
            entry.introPanel.SetActive(true);
            if (entry.pauseWhileIntro) SetPause(true);
        }

        // đóng intro theo code id
        public void CloseFeatureIntro(string codeId)
        {
            var entry = GetEntryByCodeId(codeId);
            if (entry == null) return;

            if (entry.introPanel) entry.introPanel.SetActive(false);
            if (entry.pauseWhileIntro) SetPause(false);
            entry.showIntroOnUnlock = false; // xem rồi thì thôi đừng hiện lại
        }

        private FeatureEntry GetEntryByCodeId(string codeId)
        {
            if (leadership != null && leadership.codeId == codeId) return leadership;
            if (humanResource != null && humanResource.codeId == codeId) return humanResource;
            if (deadline != null && deadline.codeId == codeId) return deadline;
            if (eventFeature != null && eventFeature.codeId == codeId) return eventFeature;
            return null;
        }

        // ===== cấu hình cho từng chức năng =====
        [Serializable]
        public class FeatureEntry
        {
            [Header("Định danh")]
            public string codeId = "feature";  // dùng code ID để gọi đóng intro

            [Header("Root UI của chức năng")]
            public GameObject root;            // bật tắt cả chức năng
            public int unlockWave = 1;         // mở khóa từ wave này trở đi

            [Header("Intro Panel (hướng dẫn)")]
            public GameObject introPanel;      // panel hướng dẫn tùy chọn
            public bool pauseWhileIntro = true;
            public bool showIntroOnUnlock = true;
            [NonSerialized] public bool wasIntroChecked = false;
        }
    }
}
