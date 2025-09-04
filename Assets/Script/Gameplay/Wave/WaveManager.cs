using System;
using UnityEngine;
using Wargency.UI;

namespace Wargency.Gameplay
{
    [DisallowMultipleComponent]
    public class WaveManager : MonoBehaviour
    {
        public static WaveManager Instance { get; private set; }

        [Header("Core")]
        [SerializeField] private WaveDefinition[] waves;
        [SerializeField] private GameLoopController glc; // vẫn dùng cho Stress/Energy/Score

        [Header("UI")]
        [SerializeField] private WaveEndPanel waveEndPanelPrefab;
        [SerializeField] private Canvas uiCanvas;

        [Header("Gameplay Refs")]
        [SerializeField] private TaskManager taskManager;
        [SerializeField] private CharacterHiringService hiringService;

        [Header("Objective Settings")]
        [SerializeField] private bool accumulateToAllSameKind = false;

        [Header("Debug")]
        [SerializeField] private bool debugSubscriptions = false;

        private int currentIndex = -1;
        public WaveDefinition CurrentWave { get; private set; }

        private bool isWaveEndPending;
        private float _timeLeft;
        private bool _timerRunning;

        private WaveEndPanel _activeWaveEndPanel;

        public event Action<int, WaveDefinition> OnWaveChanged;
        public event Action OnObjectivesUpdated;

        private int _waveStartBudget;

        public float TimeLeftSeconds => _timeLeft;
        public float TimeLimitSeconds => CurrentWave != null ? CurrentWave.timeLimitSeconds : 0f;
        public bool TimerRunning => _timerRunning;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[WaveManager] Duplicate on '{name}'. Destroy this component to avoid double counting.");
                Destroy(this);
                return;
            }
            Instance = this;

            if (!taskManager) taskManager = FindFirstObjectByType<TaskManager>();
            if (!hiringService) hiringService = FindFirstObjectByType<CharacterHiringService>();
            if (!glc) glc = FindFirstObjectByType<GameLoopController>();
            if (!uiCanvas) uiCanvas = FindFirstObjectByType<Canvas>();
        }

        private void OnEnable()
        {
            if (taskManager != null)
            {
                taskManager.OnTaskCompleted -= HandleTaskCompletedFromTaskManager;
                taskManager.OnTaskCompleted += HandleTaskCompletedFromTaskManager;
            }
            if (hiringService != null)
            {
                hiringService.OnAgentHired -= HandleAgentHired;
                hiringService.OnAgentHired += HandleAgentHired;
            }
        }

        private void OnDisable()
        {
            if (taskManager != null)
                taskManager.OnTaskCompleted -= HandleTaskCompletedFromTaskManager;
            if (hiringService != null)
                hiringService.OnAgentHired -= HandleAgentHired;
        }

        private void Start()
        {
            if (waves != null && waves.Length > 0)
                StartWave(0);
        }

        private void Update()
        {
            if (CurrentWave == null || isWaveEndPending) return;

            if (CurrentWave.useTimer && _timerRunning)
            {
                _timeLeft -= Time.deltaTime;
                if (_timeLeft <= 0f)
                {
                    _timeLeft = 0f;
                    _timerRunning = false;
                    CompleteCurrentWave();
                    return;
                }
            }

            // sample các objective mức (stress/energy/budget/score)
            if (glc != null)
            {
                bool changed = SampleTeamObjectives();
                if (changed) OnObjectivesUpdated?.Invoke();
            }

            if (!CurrentWave.useTimer && AreAllObjectivesCompleted(CurrentWave))
            {
                CompleteCurrentWave();
            }
        }

        private void StartWave(int index)
        {
            if (index < 0 || index >= waves.Length) return;

            currentIndex = index;
            CurrentWave = waves[index];
            isWaveEndPending = false;

            // timer
            if (CurrentWave.useTimer)
            {
                _timeLeft = CurrentWave.timeLimitSeconds;
                _timerRunning = true;
            }
            else _timerRunning = false;

            // reset tiến độ
            ResetObjectiveTracking(CurrentWave);

            // === Snapshot budget đầu wave từ BudgetController ===
            _waveStartBudget = (BudgetController.I != null)
                ? BudgetController.I.Balance
                : (glc != null ? glc.Budget : 0);

            // cho panel biết startBalance (dùng khi cần tự tính)
            WaveEndPanel.SetStartBalance(_waveStartBudget);

            OnWaveChanged?.Invoke(currentIndex, CurrentWave);
            OnObjectivesUpdated?.Invoke();
        }

        private void CompleteCurrentWave()
        {
            if (isWaveEndPending) return;
            isWaveEndPending = true;

            // === END - START (dùng BudgetController) ===
            int current = (BudgetController.I != null)
                ? BudgetController.I.Balance
                : (glc != null ? glc.Budget : 0);

            int earnedDelta = current - _waveStartBudget;

            string desc = ComposeWaveEndDescription(
                CurrentWave,
                earnedDelta,
                timedOut: (CurrentWave?.useTimer ?? false) && _timeLeft <= 0f
            );

            if (waveEndPanelPrefab != null && uiCanvas != null)
            {
                _activeWaveEndPanel = Instantiate(waveEndPanelPrefab, uiCanvas.transform);
                _activeWaveEndPanel.SetupWaveResult(earnedDelta, desc, AfterWaveEndContinue);
                UIManager.instance?.SetPause(true);
            }
            else
            {
                AfterWaveEndContinue();
            }
        }

        private void AfterWaveEndContinue()
        {
            if (_activeWaveEndPanel != null)
            {
                Destroy(_activeWaveEndPanel.gameObject);
                _activeWaveEndPanel = null;
            }
            UIManager.instance?.SetPause(false);

            StartWave(currentIndex + 1);

            OnWaveChanged?.Invoke(currentIndex, CurrentWave);
            OnObjectivesUpdated?.Invoke();
        }

        public WaveObjectiveDef[] GetObjectives()
            => CurrentWave != null && CurrentWave.objectives != null
                ? CurrentWave.objectives
                : Array.Empty<WaveObjectiveDef>();

        private void ResetObjectiveTracking(WaveDefinition wave)
        {
            if (wave == null || wave.objectives == null) return;
            foreach (var obj in wave.objectives)
            {
                if (obj == null) continue;
                obj.currentValue = 0;
                obj.completed = false;
            }
        }

        private bool AreAllObjectivesCompleted(WaveDefinition wave)
        {
            if (wave == null || wave.objectives == null) return false;
            foreach (var obj in wave.objectives)
            {
                if (obj == null) continue;
                if (!obj.completed) return false;
            }
            return true;
        }

        private bool SampleTeamObjectives()
        {
            if (CurrentWave == null || CurrentWave.objectives == null) return false;
            bool changed = false;

            foreach (var obj in CurrentWave.objectives)
            {
                if (obj == null) continue;
                bool before = obj.completed;

                switch (obj.kind)
                {
                    case ObjectiveKind.KeepStressBelow:
                        if (glc != null && glc.TeamStressValue <= obj.targetValue) obj.completed = true;
                        break;
                    case ObjectiveKind.KeepEnergyAbove:
                        if (glc != null && glc.TeamEnergyValue >= obj.targetValue) obj.completed = true;
                        break;
                    case ObjectiveKind.ReachBudget:
                        {
                            int bal = (BudgetController.I != null)
                                ? BudgetController.I.Balance
                                : (glc != null ? glc.Budget : 0);
                            if (bal >= obj.targetValue) obj.completed = true;
                            break;
                        }
                    case ObjectiveKind.ReachScore:
                        if (glc != null && glc.Score >= obj.targetValue) obj.completed = true;
                        break;
                }

                if (obj.completed != before) changed = true;
            }
            return changed;
        }

        private string ComposeWaveEndDescription(WaveDefinition w, int earnedDelta, bool timedOut)
        {
            string header = string.IsNullOrEmpty(w.waveDescription) ? w.displayName : w.waveDescription;

            int total = 0, completed = 0;
            var objs = GetObjectives();
            if (objs != null)
            {
                total = objs.Length;
                for (int i = 0; i < objs.Length; i++)
                    if (objs[i] != null && objs[i].completed) completed++;
            }
            int awards = completed;

            var sb = new System.Text.StringBuilder(128);
            sb.AppendLine(header);
            if (w != null && w.useTimer)
                sb.AppendLine(timedOut ? "• Hết giờ." : "• Kết thúc trong thời gian.");
            if (total > 0)
                sb.AppendLine($"• Mục tiêu: {completed}/{total} (+{awards} bút chì vàng)");
            return sb.ToString().TrimEnd();
        }

        public void ReportTaskCompleted(int count = 1) => UpdateObjective(ObjectiveKind.CompleteTasks, count);
        public void ReportHired(int count = 1) => UpdateObjective(ObjectiveKind.HireCount, count);
        public void ReportEventResolved(int count = 1) => UpdateObjective(ObjectiveKind.ResolveEvents, count);

        private void UpdateObjective(ObjectiveKind kind, int delta)
        {
            var defs = GetObjectives();
            if (defs == null || defs.Length == 0) return;

            bool changed = false;

            if (!accumulateToAllSameKind)
            {
                for (int i = 0; i < defs.Length; i++)
                {
                    var obj = defs[i];
                    if (obj == null || obj.kind != kind) continue;
                    if (obj.completed) continue;

                    int beforeInt = Mathf.FloorToInt(obj.currentValue);
                    bool beforeDone = obj.completed;

                    obj.currentValue += delta;
                    if (obj.currentValue >= obj.targetValue)
                        obj.completed = true;

                    if (beforeInt != Mathf.FloorToInt(obj.currentValue) || beforeDone != obj.completed)
                        changed = true;

                    break;
                }
            }
            else
            {
                foreach (var obj in defs)
                {
                    if (obj == null || obj.kind != kind) continue;

                    int beforeInt = Mathf.FloorToInt(obj.currentValue);
                    bool beforeDone = obj.completed;

                    obj.currentValue += delta;
                    if (obj.currentValue >= obj.targetValue)
                        obj.completed = true;

                    if (beforeInt != Mathf.FloorToInt(obj.currentValue) || beforeDone != obj.completed)
                        changed = true;
                }
            }

            if (changed) OnObjectivesUpdated?.Invoke();
        }

        private void HandleTaskCompletedFromTaskManager(TaskInstance task) => ReportTaskCompleted(1);
        private void HandleAgentHired(CharacterAgent agent) => ReportHired(1);

        public int GetCurrentWaveIndex() => currentIndex;
        public int GetTotalWaveCount() => waves != null ? waves.Length : 0;
    }
}
