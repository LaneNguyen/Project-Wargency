using System;
using UnityEngine;
using Wargency.Gameplay;
using Wargency.Systems;

namespace Wargency.UI
{
    // UI Manager tinh gọn:
    // - KHÔNG còn dev hotkeys (B/N/[/])... chỉ giữ lại Pause (Esc)
    // - WaveIntro mặc định pause theo TimeScale để "đóng băng" toàn bộ game (giống Startup Intro)
    // - UIOnlyStrategy vẫn giữ để ai cần khóa UI thay vì dừng game thì bật lại trong Inspector

    public class UIManager : MonoBehaviour, IResettable
    {
        public static UIManager instance { get; private set; }

        // ===== Pause mode =====
        public enum PauseMode { UIOnly, TimeScale }

        [Header("Pause (chung)")]
        [SerializeField] private GameObject pausePanel;                 // panel pause (nếu muốn)
        private bool isTimeScalePaused = false;                         // trạng thái pause kiểu TimeScale
        private bool isUiPaused = false;                                // trạng thái pause kiểu UIOnly

        [Header("UIOnly Lock")]
        [Tooltip("CanvasGroup của MainCanvas. Nếu có, sẽ toggle interactable/blocksRaycasts (tùy chiến lược).")]
        [SerializeField] private CanvasGroup mainCanvasGroup;           // drag CanvasGroup root vào đây
        [Tooltip("Blocker (Image full screen Raycast Target) nếu bạn không dùng CanvasGroup.")]
        [SerializeField] private GameObject uiBlocker;                  // bật/tắt để chặn click UI

        // Chiến lược khi PauseMode == UIOnly
        public enum UIOnlyStrategy { CanvasGroupOnly, BlockerOnly, Both }
        [SerializeField] private UIOnlyStrategy uiOnlyStrategy = UIOnlyStrategy.Both;

        [Header("Hotkey: Pause")]
        [SerializeField] private bool enablePauseHotkey = true;
        [SerializeField] private KeyCode keyPauseToggle = KeyCode.Escape;

        // ===== Startup Intro (tùy chọn) =====
        [Header("Startup Intro (optional)")]
        [SerializeField] private GameObject startupIntroPanel;          // panel intro lúc vào game
        [SerializeField] private bool showStartupIntro = true;          // bật/tắt intro
        [SerializeField] private PauseMode startupIntroPauseMode = PauseMode.TimeScale;
        private bool startupShown = false;

        // ===== Wave Intro & Wave Gates =====
        [Header("Wave Intro (theo wave)")]
        // Quan trọng: mặc định TimeScale để đảm bảo game "đứng hình" giống Startup Intro
        [SerializeField] private PauseMode waveIntroPauseMode = PauseMode.TimeScale;
        [SerializeField] private WaveIntroEntry[] waveIntros;           // mảng panel intro cho từng wave

        [Header("Wave Gates (mở/tắt UI theo wave)")]
        [SerializeField] private WaveGateEntry[] waveGates;             // mỗi entry bật root khi wave >= entry.wave

        //Reset
        [SerializeField] private Canvas gameplayCanvas; 
        [SerializeField] private Transform uiRuntimeRoot; // parent chứa popup/panel runtime

        private int lastKnownWave = int.MinValue;

        private void Awake()
        {
            if (instance == null) instance = this;
            else { Destroy(gameObject); return; }
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // BGM nhẹ nhàng
            AudioManager.Instance.PlayBGM(AUDIO.BGM_CHILDHOODPLAY);

            if (pausePanel) pausePanel.SetActive(false);

            // Mở UI ban đầu theo wave hiện tại
            ApplyWaveState(GetWave());

            // Startup intro (tùy chọn)
            if (showStartupIntro && !startupShown && startupIntroPanel != null)
            {
                startupShown = true;
                startupIntroPanel.SetActive(true);
                ApplyPause(startupIntroPauseMode, true);
            }
        }

        private void Update()
        {
            // Chỉ còn phím Pause
            HandlePauseHotkey();

            // Theo dõi wave thay đổi
            var current = GetWave();
            if (lastKnownWave != current)
            {
                ApplyWaveState(current);
            }
        }

        // ===== Helpers =====
        private int GetWave()
        {
            var loop = GameLoopController.Instance;
            return loop ? loop.Wave : 0;
        }

        private void ApplyWaveState(int wave)
        {
            lastKnownWave = wave;

            // 1) Mở/tắt các UI gate theo wave
            ApplyWaveGates(wave);

            // 2) Tự hiện intro của wave (mỗi wave chỉ hiện 1 lần)
            TryShowWaveIntro(wave);
        }

        // ===== Wave Gates: bật UI khi wave đủ lớn =====
        private void ApplyWaveGates(int wave)
        {
            if (waveGates == null) return;

            for (int i = 0; i < waveGates.Length; i++)
            {
                var g = waveGates[i];
                if (g == null || g.root == null) continue;
                bool unlocked = wave >= g.wave;
                g.root.SetActive(unlocked);
            }
        }

        // ===== Wave Intro =====
        private void TryShowWaveIntro(int wave)
        {
            if (waveIntros == null) return;

            for (int i = 0; i < waveIntros.Length; i++)
            {
                var e = waveIntros[i];
                if (e == null || e.panel == null) continue;
                if (e.wave == wave && e.autoShowOnEnter && !e.shown)
                {
                    e.shown = true; // chỉ hiện một lần
                    e.panel.SetActive(true);
                    ApplyPause(waveIntroPauseMode, true);
                    Debug.Log($"[UIManager] Show Wave Intro (wave {wave}) with pause = {waveIntroPauseMode} / strategy = {uiOnlyStrategy}");
                    break; // chỉ show 1 panel intro cho wave
                }
            }
        }

        public void CloseWaveIntro(int wave)
        {
            if (waveIntros == null)
            {
                ApplyPause(waveIntroPauseMode, false);
                return;
            }

            for (int i = 0; i < waveIntros.Length; i++)
            {
                var e = waveIntros[i];
                if (e == null) continue;
                if (e.wave == wave && e.panel != null)
                {
                    e.panel.SetActive(false);
                    ApplyPause(waveIntroPauseMode, false);
                    Debug.Log($"[UIManager] Close Wave Intro (wave {wave})");
                    break;
                }
            }
        }

        // ===== Startup Intro close =====
        public void CloseStartupIntro()
        {
            if (startupIntroPanel != null)
                startupIntroPanel.SetActive(false);
            ApplyPause(startupIntroPauseMode, false);
        }

        // ===== Pause logic =====
        private void ApplyPause(PauseMode mode, bool pause)
        {
            if (mode == PauseMode.TimeScale) SetTimeScalePause(pause);
            else SetUiOnlyPause(pause);
        }

        // Dừng game bằng Time.timeScale (đóng băng toàn bộ gameplay)
        private void SetTimeScalePause(bool pause)
        {
            if (isTimeScalePaused == pause) return;
            isTimeScalePaused = pause;
            Time.timeScale = pause ? 0f : 1f;
            if (pausePanel) pausePanel.SetActive(pause);
            Debug.Log(pause ? "UIManager: PAUSED (TimeScale)" : "UIManager: Resumed (TimeScale)");
        }

        // Khóa thao tác UI, game vẫn chạy
        private void SetUiOnlyPause(bool pause)
        {
            if (isUiPaused == pause) return;
            isUiPaused = pause;

            bool useCG = (uiOnlyStrategy == UIOnlyStrategy.CanvasGroupOnly || uiOnlyStrategy == UIOnlyStrategy.Both);
            bool useBlocker = (uiOnlyStrategy == UIOnlyStrategy.BlockerOnly || uiOnlyStrategy == UIOnlyStrategy.Both);

            if (useCG && mainCanvasGroup)
            {
                mainCanvasGroup.interactable = !pause;
                mainCanvasGroup.blocksRaycasts = !pause;
            }

            if (useBlocker && uiBlocker)
            {
                uiBlocker.SetActive(pause);
            }

            Debug.Log(pause
                ? $"UIManager: PAUSED (UIOnly, {uiOnlyStrategy})"
                : $"UIManager: Resumed (UIOnly, {uiOnlyStrategy})");
        }

        // ===== Only Pause hotkey =====
        private void HandlePauseHotkey()
        {
            if (!enablePauseHotkey) return;
            if (Input.GetKeyDown(keyPauseToggle))
            {
                // Toggle pause theo TimeScale để nhất quán
                SetTimeScalePause(!isTimeScalePaused);
            }
        }

        // ===== Data structs =====
        [Serializable]
        public class WaveIntroEntry
        {
            [Header("Wave")]
            public int wave = 1;

            [Header("UI Panel hiển thị khi vào wave")]
            public GameObject panel;

            [Header("Tự động hiện khi vừa vào wave")]
            public bool autoShowOnEnter = true;

            [NonSerialized] public bool shown = false;
        }

        [Serializable]
        public class WaveGateEntry
        {
            [Header("Mở root này khi wave >= value")]
            public int wave = 1;
            public GameObject root;
        }

        // ===== Compatibility cũ =====
        public void SetPause(bool pause)
        {
            SetTimeScalePause(pause);
        }

        public void ResetState()
        {
            var root = uiRuntimeRoot != null ? uiRuntimeRoot : (gameplayCanvas ? gameplayCanvas.transform : transform);
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                if (child != null) Destroy(child.gameObject);
            }

            SetPause(false);
        }
    }
}

