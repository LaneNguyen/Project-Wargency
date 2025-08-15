using System;
using Unity.VisualScripting;
using UnityEngine;
using Wargency.Gameplay;

namespace Wargency.UI
{
    // UIManager gom các UI Stub (Budget/Score/Wave), bao gồm function:
    // - Dev Hotkeys để test nhanh gameplay loop (B/N/[ / ])
    // - Pause/Resume bằng phím Esc (tạm thời dùng Time.timeScale)
    // - (Optional) Tham chiếu tới các stub để tiện kiểm tra trong Inspector
    // Note nhẹ:
    // - Các UIBudgetStub/UIScoreStub/UIWaveStub TỰ cập nhật qua event từ GameLoopController.
    //   UIManager không can thiệp vào việc update text.
    // - Mục tiêu là tổ chức & điều phối UI, tạo tiện ích test mà không làm rối logic
    public class UIManager : MonoBehaviour
    {
        public static UIManager instance { get; private set; }

        [Header("Khu vực tham chiếu")]
        [Tooltip("Tham chiếu UIBudgetStub")]
        [SerializeField] private UIBudgetStub budgetStub;
        [SerializeField] private UIScoreStub scoreStub;
        [SerializeField] private UIWaveStub waveStub;

        [Header("Nút Pause")]
        [SerializeField] private GameObject pausePanel;

        [Header("Developer Hotkeys")]
        [Tooltip("Bật tắt nút test nhanh khi dev")]
        [SerializeField] private bool enableDevHotkey = true;

        [Tooltip("Nút cộng budget - nút B mặc định")]
        [SerializeField] private KeyCode keyAddBudget = KeyCode.B;

        [Tooltip("Nút trừ và xài tiền - Mặc định: N")]
        [SerializeField] private KeyCode keySpendBudget = KeyCode.N;

        [Tooltip("Phím giảm wave - Mặc định [")]
        [SerializeField] private KeyCode keyWavedecrease = KeyCode.LeftBracket;

        [Tooltip("Phím tăng wave - Mặc định ]")]
        [SerializeField] private KeyCode keyWaveincrease = KeyCode.RightBracket;

        [Tooltip("Phím pause/resume - Mặc định Esc")]
        [SerializeField] private KeyCode keyPausetoggle = KeyCode.Escape;

        // Trạng thái pause cục bộ cho UIManager (M1: chỉ dùng Time.timeScale)
        private bool isPaused = false;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else { Destroy(this.gameObject); }
            DontDestroyOnLoad(this.gameObject);
        }
        private void Start()
        {             
            // Ẩn panel Pause lúc bắt đầu (nếu có gán)
            if (pausePanel !=null)
            {
                pausePanel.SetActive(false);
            }

            // Log nhẹ để check đã sẵn sàng hay chưa
            if (GameLoopController.Instance == null)
            {
                Debug.LogWarning("[UIManager] GameLoopController.Instance chưa tồn tại trong scene.");
            }

        }

        private void Update()
        {
            HandleHotkeys();
        }

        // Xử lý các phím tắt dành cho dev/QA để test nhanh dòng chảy M1.
        private void HandleHotkeys()
        {
            if (!enableDevHotkey) return;
            // An toàn: chỉ xử lý khi GameLoopController đã sẵn sàng
            var gameloopcontroller = GameLoopController.Instance;
            if (gameloopcontroller == null) { return; }

            //Add budget (+100)
            if(Input.GetKeyDown(keyAddBudget))
            {
                gameloopcontroller.AddBudget(100);
                Debug.Log("[UIManager] +100 Budget (dev hotkey).");
            }

            // Xài 50 tiền nếu đủ
            if(Input.GetKeyDown(keySpendBudget))
            {
                bool tryspend = gameloopcontroller.TrySpendBudget(50);
                Debug.Log(tryspend ? "[UIManager] -50 Budget (dev hotkey)."
                              : "[UIManager] Không đủ Budget để trừ 50.");
            }

            // Wave giảm
            if(Input.GetKeyDown(keyWavedecrease))
            {
                int currentwave = gameloopcontroller.Wave;
                gameloopcontroller.SetWave(currentwave - 1);
                Debug.Log($"[UIManager] Wave -> {gameloopcontroller.Wave} (dev hotkey)");
            }

            //Wave tăng
            if (Input.GetKeyDown(keyWaveincrease))
            {
                int currentwave = gameloopcontroller.Wave;
                gameloopcontroller.SetWave(currentwave + 1);
                Debug.Log($"[UIManager] Wave -> {gameloopcontroller.Wave} (dev hotkey).");
            }

            //Pause hoặc resume
            if (Input.GetKeyDown(keyPausetoggle))
            {
                TogglePause();
            }
        }

        // Bật/tắt Pause đơn giản bằng Time.timeScale (M1).
        // - Pause: timeScale = 0 (dừng Update/FixedUpdate dựa trên Time.deltaTime)
        // - Resume: timeScale = 1
        // - Hiện/ẩn pausePanel nếu có
        // note trước:
        // - Tạm thời cho M1. Sang M2 có thể chuyển sang cơ chế Pause thực thụ qua GameLoopController (thêm state Pause).
        public void TogglePause()
        {
            isPaused = !isPaused;
            Time.timeScale = isPaused ? 0f : 1f;

            if(pausePanel != null)
            {
                pausePanel.SetActive(isPaused);
            }
            Debug.Log(isPaused ? "UI Manager: PAUSED" : "UI MANAGER: Resumed");
        }

        // API thêm để mở/tắt pause từ nơi khác (vd: nút UI).
        public void SetPause(bool pause)
        {
            if (isPaused == pause) return;
            isPaused = pause;
            Time.timeScale = isPaused ? 0f : 1f;

            if (pausePanel != null)
            {
                pausePanel.SetActive(isPaused);
            }
            Debug.Log(isPaused ? "UI Manager: PAUSED" : "UI MANAGER: Resumed");
        }

    }
}
