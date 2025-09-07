using System;
using System.Reflection; // [PATCH DEBUG] fix BindingFlags usage
using UnityEngine;
using Wargency.Systems;
using Wargency.UI;

namespace Wargency.Gameplay
{
    [DisallowMultipleComponent]
    public class WaveManager : MonoBehaviour, IResettable
    {
        public static WaveManager Instance { get; private set; }

        [Header("Core")]
        [SerializeField] private WaveDefinition[] waves;
        [SerializeField] private GameLoopController glc; // vẫn dùng cho Stress/Energy/Score

        [Header("UI")]
        [SerializeField] private WaveEndPanel waveEndPanelPrefab;
        [SerializeField] private Canvas uiCanvas;

        [Header("End Game")]
        [SerializeField] private EndgamePanel endgamePanelPrefab; // KÉO PREFAB VÀO

        [Header("Gameplay Refs")]
        [SerializeField] private TaskManager taskManager;
        [SerializeField] private CharacterHiringService hiringService;

        // [PATCH OBJECTIVE] Nghe Event resolve để cộng KPI
        [SerializeField] private EventManager eventManager;

        [Header("Objective Settings")]
        [SerializeField] private bool accumulateToAllSameKind = false;

        [Header("Audio (via AudioManager)")]
        [Tooltip("SE phát khi mở các panel (WaveEnd/Endgame).")]
        [SerializeField] private string seOpenPanel = "PanelOpen";
        [Tooltip("SE click dùng chung (phần click tại EndgamePanel).")]
        [SerializeField] private string seClick = "ButtonClick";

        [Header("Debug")]
        [SerializeField] private bool debugSubscriptions = false;
        // [PATCH DEBUG] Bật theo dõi budget delta theo wave (log khi thay đổi)
        [SerializeField] private bool debugBudgetTrace = true;
        // [PATCH DEBUG] lưu lần cuối đã log để tránh spam
        private int _dbgLastBal = int.MinValue;
        private int _dbgLastDelta = int.MinValue;

        [ContextMenu("Force Endgame Now")]
        private void _ContextForceEndgame() => ForceShowEndgame();

        private int currentIndex = -1;
        public WaveDefinition CurrentWave { get; private set; }

        private bool isWaveEndPending;
        private float _timeLeft;
        private bool _timerRunning;

        private WaveEndPanel _activeWaveEndPanel;
        private EndgamePanel _activeEndgamePanel;

        public event Action<int, WaveDefinition> OnWaveChanged;
        public event Action OnObjectivesUpdated;

        private int _waveStartBudget;

        public float TimeLeftSeconds => _timeLeft;
        public float TimeLimitSeconds => CurrentWave != null ? CurrentWave.timeLimitSeconds : 0f;
        public bool TimerRunning => _timerRunning;

        // === PATCH: tag để tìm Canvas trong scene nếu uiCanvas bị gán nhầm prefab
        [SerializeField] private string uiCanvasTag = "MainCanvas"; // optional

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
            if (!eventManager) eventManager = FindFirstObjectByType<EventManager>(); // [PATCH OBJECTIVE]

            // === PATCH: nếu uiCanvas đang trỏ tới prefab (persistent) thì bỏ và tự tìm Canvas trong scene
            if (uiCanvas != null && !uiCanvas.gameObject.scene.IsValid())
            {
                if (debugSubscriptions) Debug.Log("[WaveManager] uiCanvas is persistent (prefab). Rebinding to scene Canvas…");
                uiCanvas = null;
            }
            if (!uiCanvas)
            {
                // thử theo tag
                if (!string.IsNullOrEmpty(uiCanvasTag))
                {
                    var tagged = GameObject.FindWithTag(uiCanvasTag);
                    if (tagged && tagged.scene.IsValid())
                        uiCanvas = tagged.GetComponent<Canvas>();
                }
                // fallback: lấy Canvas đầu tiên trong scene
                if (!uiCanvas)
                    uiCanvas = FindFirstObjectByType<Canvas>();
            }
        }

        private void OnEnable()
        {
            if (taskManager != null)
            {
                if (debugSubscriptions) Debug.Log("[WaveManager] Sub to TaskManager.OnTaskCompleted");
                taskManager.OnTaskCompleted -= HandleTaskCompletedFromTaskManager;
                taskManager.OnTaskCompleted += HandleTaskCompletedFromTaskManager;
            }
            if (hiringService != null)
            {
                if (debugSubscriptions) Debug.Log("[WaveManager] Sub to HiringService.OnAgentHired");
                hiringService.OnAgentHired -= HandleAgentHired;
                hiringService.OnAgentHired += HandleAgentHired;
            }
            if (eventManager != null)
            {
                if (debugSubscriptions) Debug.Log("[WaveManager] Sub to EventManager.OnEventResolved");
                eventManager.OnEventResolved -= HandleEventResolvedFromEventManager; // tránh double
                eventManager.OnEventResolved += HandleEventResolvedFromEventManager;
            }
        }

        private void OnDisable()
        {
            if (taskManager != null)
            {
                if (debugSubscriptions) Debug.Log("[WaveManager] Unsub TaskManager.OnTaskCompleted");
                taskManager.OnTaskCompleted -= HandleTaskCompletedFromTaskManager;
            }
            if (hiringService != null)
            {
                if (debugSubscriptions) Debug.Log("[WaveManager] Unsub HiringService.OnAgentHired");
                hiringService.OnAgentHired -= HandleAgentHired;
            }
            if (eventManager != null)
            {
                if (debugSubscriptions) Debug.Log("[WaveManager] Unsub EventManager.OnEventResolved");
                eventManager.OnEventResolved -= HandleEventResolvedFromEventManager;
            }
        }

        private void Start()
        {
            if (waves != null && waves.Length > 0)
                StartWave(0);
            else
                Debug.LogWarning("[WaveManager] No waves configured.");
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
                    if (debugSubscriptions) Debug.Log("[WaveManager] Timer hit zero → CompleteCurrentWave()");
                    CompleteCurrentWave();
                    return;
                }
            }

            // sample các objective mức (stress/energy/budget/score)
            bool changed = SampleTeamObjectives();
            if (changed) OnObjectivesUpdated?.Invoke();

            if (!CurrentWave.useTimer && AreAllObjectivesCompleted(CurrentWave))
            {
                if (debugSubscriptions) Debug.Log("[WaveManager] All objectives completed (no timer) → CompleteCurrentWave()");
                CompleteCurrentWave();
            }
        }

        private void StartWave(int index)
        {
            if (index < 0 || index >= waves.Length)
            {
                Debug.LogWarning($"[WaveManager] StartWave index out of range: {index}");
                return;
            }

            currentIndex = index;
            CurrentWave = waves[index];
            isWaveEndPending = false;

            if (debugSubscriptions) Debug.Log($"[WaveManager] >>> StartWave {currentIndex + 1}/{waves.Length}: {CurrentWave?.displayName}");

            // Đồng bộ wave về GameLoopController (1-based)
            if (glc != null) glc.SetWave(index + 1);

            // timer
            if (CurrentWave.useTimer)
            {
                _timeLeft = CurrentWave.timeLimitSeconds;
                _timerRunning = true;
            }
            else _timerRunning = false;

            // reset tiến độ
            ResetObjectiveTracking(CurrentWave);

            // Snapshot budget đầu wave
            _waveStartBudget = (BudgetController.I != null)
                ? BudgetController.I.Balance
                : (glc != null ? glc.Budget : 0);

            WaveEndPanel.SetStartBalance(_waveStartBudget);

            // [PATCH DEBUG] log snapshot đầu wave
            if (debugSubscriptions || debugBudgetTrace)
            {
                Debug.Log($"[WaveManager][ReachBudget] StartWave idx={currentIndex} startBudget={_waveStartBudget}");
                _dbgLastBal = int.MinValue; // reset để lần check đầu log chắc chắn
                _dbgLastDelta = int.MinValue;
            }

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

            // [PATCH DEBUG] log tổng kết wave
            if (debugSubscriptions || debugBudgetTrace)
            {
                Debug.Log($"[WaveManager][ReachBudget] CompleteWave idx={currentIndex} endBal={current}, startBal={_waveStartBudget}, delta={earnedDelta}");
            }

            // Tính "bút chì vàng" của wave = số objective hoàn thành / tổng
            int totalObj = 0, completedObj = 0;
            var objs = GetObjectives();
            if (objs != null)
            {
                totalObj = objs.Length;
                for (int i = 0; i < objs.Length; i++)
                    if (objs[i] != null && objs[i].completed) completedObj++;
            }

            // Cộng dồn vào GameResult (ghi đè theo wave khi replay)
            var grc = FindFirstObjectByType<GameResultController>();
            if (grc != null)
            {
                grc.AddOrReplaceWaveResult(currentIndex, completedObj, totalObj);
            }

            string desc = ComposeWaveEndDescription(
                CurrentWave,
                earnedDelta,
                timedOut: (CurrentWave?.useTimer ?? false) && _timeLeft <= 0f
            );

            bool isLastWave = (currentIndex + 1) >= (waves != null ? waves.Length : 0);

            if (isLastWave)
            {
                if (debugSubscriptions) Debug.Log($"[WaveManager] Completed LAST wave {currentIndex + 1}. Open Endgame immediately.");
                ShowEndgame();
                return;
            }

            // === PATCH: dùng parent thuộc scene (không dùng prefab) ===
            var parentForWaveEnd = GetSceneCanvasTransform();

            if (waveEndPanelPrefab != null && parentForWaveEnd != null)
            {
                if (debugSubscriptions) Debug.Log($"[WaveManager] Open WaveEndPanel for wave {currentIndex + 1}. earnedDelta={earnedDelta}");

                // Play SE khi mở WaveEndPanel
                AudioManager.Instance?.PlaySE(AUDIO.SE_GOODRESULT);

                _activeWaveEndPanel = Instantiate(waveEndPanelPrefab, parentForWaveEnd);
                _activeWaveEndPanel.SetupWaveResult(earnedDelta, desc, AfterWaveEndContinue);
                UIManager.instance?.SetPause(true);
            }
            else
            {
                Debug.LogWarning("[WaveManager] WaveEndPanel prefab/canvas missing → fallback AfterWaveEndContinue()");
                AfterWaveEndContinue();
            }
        }

        private void AfterWaveEndContinue()
        {
            if (_activeWaveEndPanel != null)
            {
                if (debugSubscriptions) Debug.Log($"[WaveManager] Close WaveEndPanel for wave {currentIndex + 1}");
                Destroy(_activeWaveEndPanel.gameObject);
                _activeWaveEndPanel = null;
            }

            int nextIndex = currentIndex + 1;
            if (waves == null || nextIndex >= waves.Length)
            {
                if (debugSubscriptions) Debug.Log("[WaveManager] No next wave → ShowEndgame() (fallback path)");
                ShowEndgame();
                return;
            }

            UIManager.instance?.SetPause(false);
            if (debugSubscriptions) Debug.Log($"[WaveManager] Continue to next wave: {nextIndex + 1}");
            StartWave(nextIndex);

            OnWaveChanged?.Invoke(currentIndex, CurrentWave);
            OnObjectivesUpdated?.Invoke();
        }

        private void ShowEndgame()
        {
            UIManager.instance?.SetPause(true);

            var grc = FindFirstObjectByType<GameResultController>();
            int totalEarned = grc != null ? grc.totalPencilsEarned : 0;
            int totalPossible = grc != null ? grc.totalPencilsPossible : 0;

            // === PATCH: dùng parent thuộc scene (không dùng prefab) ===
            var parentForEndgame = GetSceneCanvasTransform();

            if (endgamePanelPrefab != null && parentForEndgame != null)
            {
                if (debugSubscriptions) Debug.Log($"[WaveManager] >>> ShowEndgame() totalEarned={totalEarned}/{totalPossible}");

                // Play SE khi mở EndgamePanel
                AudioManager.Instance?.PlaySE(AUDIO.SE_GOODRESULT);

                _activeEndgamePanel = Instantiate(endgamePanelPrefab, parentForEndgame);

                // Dùng bản có replay để hiện nút chơi lại
                _activeEndgamePanel.SetupWithReplay(
                    totalEarned,
                    totalPossible,
                    OnEndgameExit,
                    ReplayFromWave3,
                    ReplayFromWave4
                );
            }
            else
            {
                Debug.LogWarning("[WaveManager] EndgamePanel prefab/canvas chưa gán.");
                OnEndgameExit();
            }
        }

        // === PATCH: helper đảm bảo luôn trả về Transform của Canvas trong SCENE
        private Transform GetSceneCanvasTransform()
        {
            // ưu tiên uiCanvas nếu là scene object
            if (uiCanvas && uiCanvas.gameObject.scene.IsValid())
                return uiCanvas.transform;

            // thử theo tag
            if (!string.IsNullOrEmpty(uiCanvasTag))
            {
                var tagged = GameObject.FindWithTag(uiCanvasTag);
                if (tagged && tagged.scene.IsValid())
                {
                    uiCanvas = tagged.GetComponent<Canvas>();
                    if (uiCanvas) return uiCanvas.transform;
                }
            }

            // fallback: lấy Canvas đầu tiên trong scene
            var any = FindFirstObjectByType<Canvas>();
            if (any && any.gameObject.scene.IsValid())
            {
                uiCanvas = any; // cache lại
                return any.transform;
            }

            // hết cách → null (sẽ không Instantiate với parent persistent)
            return null;
        }

        private void OnEndgameExit()
        {
            if (debugSubscriptions) Debug.Log("[WaveManager] OnEndgameExit()");
            UIManager.instance?.SetPause(false);
        }

        public void ForceShowEndgame()
        {
            if (debugSubscriptions) Debug.Log("[WaveManager] ForceShowEndgame()");

            if (_activeWaveEndPanel != null)
            {
                Destroy(_activeWaveEndPanel.gameObject);
                _activeWaveEndPanel = null;
            }

            ShowEndgame();
        }

        public void ReplayFromWave3() => RestartFromWave(2, clearResultsFromWaveIndex: 2);
        public void ReplayFromWave4() => RestartFromWave(3, clearResultsFromWaveIndex: 3);

        public void RestartFromWave(int startIndex, int? clearResultsFromWaveIndex = null)
        {
            if (_activeEndgamePanel != null)
            {
                Destroy(_activeEndgamePanel.gameObject);
                _activeEndgamePanel = null;
            }

            var grc = FindFirstObjectByType<GameResultController>();
            if (grc != null && clearResultsFromWaveIndex.HasValue)
            {
                grc.ClearFromWave(clearResultsFromWaveIndex.Value);
            }

            UIManager.instance?.SetPause(false);
            int clamped = Mathf.Clamp(startIndex, 0, (waves != null ? waves.Length - 1 : 0));
            if (debugSubscriptions) Debug.Log($"[WaveManager] RestartFromWave({clamped})");
            StartWave(clamped);
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
            if (debugSubscriptions) Debug.Log("[WaveManager] ResetObjectiveTracking()");
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

            // [PATCH OBJECTIVE] Tính cực trị realtime nếu cần
            float stressMax, energyMin;
            bool hasExtremes = TryGetTeamExtremes(out stressMax, out energyMin);

            foreach (var obj in CurrentWave.objectives)
            {
                if (obj == null) continue;
                bool before = obj.completed;

                switch (obj.kind)
                {
                    case ObjectiveKind.KeepStressBelow:
                        {
                            float s = hasExtremes ? stressMax : (glc != null ? glc.TeamStressValue : 0f);
                            bool newCompleted = (s <= obj.targetValue);
                            if (obj.completed != newCompleted) { obj.completed = newCompleted; changed = true; }
                            break;
                        }
                    case ObjectiveKind.KeepEnergyAbove:
                        {
                            float e = hasExtremes ? energyMin : (glc != null ? glc.TeamEnergyValue : 0f);
                            bool newCompleted = (e >= obj.targetValue);
                            if (obj.completed != newCompleted) { obj.completed = newCompleted; changed = true; }
                            break;
                        }
                    case ObjectiveKind.ReachBudget:
                        {
                            int bal = (BudgetController.I != null)
                                ? BudgetController.I.Balance
                                : (glc != null ? glc.Budget : 0);
                            int delta = bal - _waveStartBudget;

                            if ((debugSubscriptions || debugBudgetTrace) &&
                                (bal != _dbgLastBal || delta != _dbgLastDelta))
                            {
                                Debug.Log($"[WaveManager][ReachBudget] bal={bal}, start={_waveStartBudget}, delta={delta}, target={obj.targetValue}");
                                _dbgLastBal = bal;
                                _dbgLastDelta = delta;
                            }

                            bool newCompleted = (delta >= obj.targetValue);
                            if (obj.completed != newCompleted) { obj.completed = newCompleted; changed = true; }
                            break;
                        }
                    case ObjectiveKind.ReachScore:
                        {
                            bool newCompleted = (glc != null && glc.Score >= obj.targetValue);
                            if (obj.completed != newCompleted) { obj.completed = newCompleted; changed = true; }
                            break;
                        }
                        // Các objective kiểu đếm (task/hire/event) giữ nguyên cơ chế Report*().
                }
            }
            return changed;
        }

        // [PATCH OBJECTIVE] Lấy MAX Stress & MIN Energy realtime bằng TaskManager + reflection (an toàn biên dịch)
        private bool TryGetTeamExtremes(out float stressMax, out float energyMin)
        {
            stressMax = 0f;
            energyMin = float.MaxValue;
            bool any = false;

            if (taskManager != null && taskManager.ActiveAgents != null)
            {
                for (int i = 0; i < taskManager.ActiveAgents.Count; i++)
                {
                    var a = taskManager.ActiveAgents[i];
                    if (a == null) continue;

                    var stats = a.GetComponent<CharacterStats>();
                    if (stats == null) continue;

                    float s = ReadFloat(stats, "CurrentStress", "Stress", "stress");
                    float e = ReadFloat(stats, "CurrentEnergy", "Energy", "energy");

                    if (s > stressMax) stressMax = s;
                    if (e < energyMin) energyMin = e;
                    any = true;
                }
            }

            if (!any) { energyMin = 0f; return false; }
            if (energyMin == float.MaxValue) energyMin = 0f;
            return true;
        }

        private static float ReadFloat(object obj, params string[] names)
        {
            if (obj == null) return 0f;
            var t = obj.GetType();

            foreach (var n in names)
            {
                var p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.PropertyType == typeof(int)) return (int)p.GetValue(obj);
                if (p != null && p.PropertyType == typeof(float)) return (float)p.GetValue(obj);
            }
            foreach (var n in names)
            {
                var f = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null && f.FieldType == typeof(int)) return (int)f.GetValue(obj);
                if (f != null && f.FieldType == typeof(float)) return (float)f.GetValue(obj);
            }
            return 0f;
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

        private void HandleTaskCompletedFromTaskManager(TaskInstance task)
        {
            if (debugSubscriptions) Debug.Log("[WaveManager] ReportTaskCompleted(+1)");
            ReportTaskCompleted(1);
        }

        private void HandleAgentHired(CharacterAgent agent)
        {
            if (debugSubscriptions) Debug.Log("[WaveManager] ReportHired(+1)");
            ReportHired(1);
        }

        // [PATCH OBJECTIVE] Khi Event kết thúc, cộng +1 ResolveEvents
        private void HandleEventResolvedFromEventManager()
        {
            if (debugSubscriptions) Debug.Log("[WaveManager] ReportEventResolved(+1) via EventManager");
            ReportEventResolved(1);
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
            sb.AppendLine($"• Kết quả ngân sách: {(earnedDelta >= 0 ? "+" : "")}{earnedDelta}");
            return sb.ToString().TrimEnd();
        }

        public int GetCurrentWaveIndex() => currentIndex;
        public int GetTotalWaveCount() => waves != null ? waves.Length : 0;

        public void ResetState()
        {
            UIManager.instance?.SetPause(false);

            if (_activeWaveEndPanel) Destroy(_activeWaveEndPanel.gameObject);
            if (_activeEndgamePanel) Destroy(_activeEndgamePanel.gameObject);
            _activeWaveEndPanel = null;
            _activeEndgamePanel = null;

            CurrentWave = null;
            currentIndex = -1;
            isWaveEndPending = false;
            _timerRunning = false;
            _timeLeft = 0f;
            _waveStartBudget = 0;

            if (taskManager) taskManager.OnTaskCompleted -= HandleTaskCompletedFromTaskManager;
            if (hiringService) hiringService.OnAgentHired -= HandleAgentHired;
        }
    }
}
