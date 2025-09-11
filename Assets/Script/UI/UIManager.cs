using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wargency.Gameplay;
using Wargency.Systems;

namespace Wargency.UI
{
    public class UIManager : BaseManager<UIManager>, IResettable
    {
        public static UIManager instance { get; private set; }

        public enum PauseMode { UIOnly, TimeScale }

        [Header("Pause")]
        [SerializeField] private GameObject pausePanel;
        private bool isTimeScalePaused = false;
        private bool isUiPaused = false;

        [Header("UIOnly Lock")]
        [SerializeField] private CanvasGroup mainCanvasGroup;
        [SerializeField] private GameObject uiBlocker;
        public enum UIOnlyStrategy { CanvasGroupOnly, BlockerOnly, Both }
        [SerializeField] private UIOnlyStrategy uiOnlyStrategy = UIOnlyStrategy.Both;

        [Header("Hotkey: Pause")]
        [SerializeField] private bool enablePauseHotkey = true;
        [SerializeField] private KeyCode keyPauseToggle = KeyCode.Escape;

        [Header("Startup Intro")]
        [SerializeField] private GameObject startupIntroPanel;
        [SerializeField] private bool showStartupIntro = true;
        [SerializeField] private PauseMode startupIntroPauseMode = PauseMode.TimeScale;
        private bool startupShown = false;

        [Header("Wave Intro")]
        [SerializeField] private PauseMode waveIntroPauseMode = PauseMode.TimeScale;
        [SerializeField] private WaveIntroEntry[] waveIntros;

        [Header("Wave Gates")]
        [SerializeField] private WaveGateEntry[] waveGates;

        [SerializeField] private Canvas gameplayCanvas;
        [SerializeField] private Transform uiRuntimeRoot;
        private int lastKnownWave = int.MinValue;

        [Header("Settings")]
        [SerializeField] private GameObject settingPanel;
        [SerializeField] private bool pauseWhenSettingsOpen = true;

        // -------- Auto Assign (toggle 2-state) --------
        [Header("Feature: Auto Assign")]
        [SerializeField] private int autoAssignUnlockWave = 2;
        [SerializeField] private GameObject autoAssignFeatureRoot;
        [SerializeField] private Button autoAssignButton;
        [SerializeField] private CanvasGroup autoAssignButtonCanvasGroup;

        [Header("Cost")]
        [SerializeField] private int autoAssignUnlockCost = 500;
        [SerializeField] private bool enableAutoAssignAfterUnlock = true;

        [Header("State Animator (2-state)")]
        [SerializeField] private Animator autoAssignStateAnimator;
        [SerializeField] private string stateBoolName = "AutoAssignOn";

        private bool autoAssignUnlocked = false;

        // chặn double-click cùng frame
        private bool autoAssignClickGuard = false;
        private Coroutine autoAssignGuardCR;
        // ----------------------------------------------

        private void Awake()
        {
            if (instance == null) instance = this;
            else { Destroy(gameObject); return; }
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            AudioManager.Instance.PlayBGM(AUDIO.BGM_CHILDHOODPLAY);

            if (pausePanel) pausePanel.SetActive(false);
            if (settingPanel) settingPanel.SetActive(false);

            if (autoAssignButton != null)
            {
                autoAssignButton.onClick.RemoveAllListeners();
                autoAssignButton.onClick.AddListener(OnClick_AutoAssignButton);
                autoAssignButton.transition = Selectable.Transition.ColorTint;
                autoAssignButton.navigation = new Navigation { mode = Navigation.Mode.None };
            }

            if (autoAssignStateAnimator == null && autoAssignButton != null)
            {
                autoAssignStateAnimator = autoAssignButton.GetComponent<Animator>();
                if (autoAssignStateAnimator == null)
                    autoAssignStateAnimator = autoAssignButton.GetComponentInChildren<Animator>(true);
            }

            ApplyWaveState(GetWave());

            if (showStartupIntro && !startupShown && startupIntroPanel != null)
            {
                startupShown = true;
                startupIntroPanel.SetActive(true);
                ApplyPause(startupIntroPauseMode, true);
            }

            SyncAutoAssignUIFromState();
        }

        private void Update()
        {
            HandlePauseHotkey();

            var current = GetWave();
            if (lastKnownWave != current)
            {
                ApplyWaveState(current);
            }
        }

        private int GetWave()
        {
            var loop = GameLoopController.Instance;
            return loop ? loop.Wave : 0;
        }

        private void ApplyWaveState(int wave)
        {
            lastKnownWave = wave;
            ApplyWaveGates(wave);
            TryShowWaveIntro(wave);
            ApplyAutoAssignGate(wave);
            RefreshAutoAssignButtonVisual();
        }

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

        private void ApplyAutoAssignGate(int wave)
        {
            if (autoAssignFeatureRoot == null) return;
            bool canShow = wave >= autoAssignUnlockWave;
            autoAssignFeatureRoot.SetActive(canShow);
        }

        private void TryShowWaveIntro(int wave)
        {
            if (waveIntros == null) return;
            for (int i = 0; i < waveIntros.Length; i++)
            {
                var e = waveIntros[i];
                if (e == null || e.panel == null) continue;
                if (e.wave == wave && e.autoShowOnEnter && !e.shown)
                {
                    e.shown = true;
                    e.panel.SetActive(true);
                    ApplyPause(waveIntroPauseMode, true);
                    break;
                }
            }
        }

        public void CloseWaveIntro(int wave)
        {
            if (waveIntros == null) { ApplyPause(waveIntroPauseMode, false); return; }
            for (int i = 0; i < waveIntros.Length; i++)
            {
                var e = waveIntros[i];
                if (e == null) continue;
                if (e.wave == wave && e.panel != null)
                {
                    e.panel.SetActive(false);
                    ApplyPause(waveIntroPauseMode, false);
                    break;
                }
            }
        }

        public void CloseStartupIntro()
        {
            if (startupIntroPanel != null) startupIntroPanel.SetActive(false);
            ApplyPause(startupIntroPauseMode, false);
        }

        private void ApplyPause(PauseMode mode, bool pause)
        {
            if (mode == PauseMode.TimeScale) SetTimeScalePause(pause);
            else SetUiOnlyPause(pause);
        }

        private void SetTimeScalePause(bool pause)
        {
            if (isTimeScalePaused == pause) return;
            isTimeScalePaused = pause;
            Time.timeScale = pause ? 0f : 1f;
            if (pausePanel) pausePanel.SetActive(pause);
        }

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
        }

        private void HandlePauseHotkey()
        {
            if (!enablePauseHotkey) return;
            if (Input.GetKeyDown(keyPauseToggle)) ToggleSettings();
        }

        public void OpenSettingsFromButton() => ToggleSettings();

        public void ToggleSettings()
        {
            if (!settingPanel) return;
            bool toActive = !settingPanel.activeSelf;
            settingPanel.SetActive(toActive);
            if (pauseWhenSettingsOpen) SetTimeScalePause(toActive);
            AudioManager.Instance.PlaySE(AUDIO.SE_BUTTONCLICK);
        }

        public void CloseSettings()
        {
            if (!settingPanel) return;
            settingPanel.SetActive(false);
            if (pauseWhenSettingsOpen) SetTimeScalePause(false);
        }

        [Serializable]
        public class WaveIntroEntry
        {
            public int wave = 1;
            public GameObject panel;
            public bool autoShowOnEnter = true;
            [NonSerialized] public bool shown = false;
        }

        [Serializable]
        public class WaveGateEntry
        {
            public int wave = 1;
            public GameObject root;
        }

        public void SetPause(bool pause) => SetTimeScalePause(pause);

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

        // -------- Auto Assign: click to toggle + set Animator bool (debounced) --------
        public void OnClick_AutoAssignButton()
        {
            if (autoAssignClickGuard) return; // double-call guard
            autoAssignClickGuard = true;
            if (autoAssignGuardCR != null) StopCoroutine(autoAssignGuardCR);
            autoAssignGuardCR = StartCoroutine(CoClearAutoAssignGuard());

            var tm = TaskManager.Instance != null ? TaskManager.Instance : FindObjectOfType<TaskManager>(true);
            if (tm == null) return;

            if (GetWave() < autoAssignUnlockWave)
            {
                AudioManager.Instance.PlaySE(AUDIO.SE_BUTTONCLICK);
                return;
            }

            if (!autoAssignUnlocked && !tm.autoAssignUnlocked)
            {
                if (BudgetController.I == null) return;

                bool paid = BudgetController.I.TrySpend(autoAssignUnlockCost);
                if (!paid)
                {
                    AudioManager.Instance.PlaySE(AUDIO.SE_BUTTONCLICK);
                    return;
                }

                autoAssignUnlocked = true;
                tm.UnlockAutoAssignFeature();
                AudioManager.Instance.PlaySE(AUDIO.SE_BUTTONCLICK);
                RefreshAutoAssignButtonVisual();

                if (enableAutoAssignAfterUnlock)
                {
                    tm.SetAutoAssignEnabled(true);
                }

                ApplyAutoAssignVisualState(tm.autoAssignEnabled);
                return;
            }

            bool next = !tm.autoAssignEnabled;
            tm.SetAutoAssignEnabled(next);
            AudioManager.Instance.PlaySE(AUDIO.SE_BUTTONCLICK);
            ApplyAutoAssignVisualState(next);
        }

        private IEnumerator CoClearAutoAssignGuard()
        {
            yield return null; // clear next frame
            autoAssignClickGuard = false;
        }

        private void SyncAutoAssignUIFromState()
        {
            var tm = TaskManager.Instance != null ? TaskManager.Instance : FindObjectOfType<TaskManager>(true);
            if (tm == null) return;

            autoAssignUnlocked = tm.autoAssignUnlocked;
            RefreshAutoAssignButtonVisual();
            ApplyAutoAssignGate(GetWave());
            ApplyAutoAssignVisualState(tm.autoAssignEnabled);
        }

        private void RefreshAutoAssignButtonVisual()
        {
            if (autoAssignButtonCanvasGroup == null) return;
            var tm = TaskManager.Instance != null ? TaskManager.Instance : FindObjectOfType<TaskManager>(true);
            bool unlocked = autoAssignUnlocked || (tm != null && tm.autoAssignUnlocked);
            autoAssignButtonCanvasGroup.alpha = unlocked ? 1f : 0.6f;
            if (autoAssignButton != null) autoAssignButton.interactable = true;
        }

        private void ApplyAutoAssignVisualState(bool isOn)
        {
            if (autoAssignStateAnimator != null && !string.IsNullOrEmpty(stateBoolName))
                autoAssignStateAnimator.SetBool(stateBoolName, isOn);
        }
    }
}