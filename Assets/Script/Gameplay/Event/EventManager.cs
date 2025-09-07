using System;
using System.Linq;
using UnityEngine;
using Wargency.Systems;
using Random = UnityEngine.Random;

namespace Wargency.Gameplay
{
    // Quản lý random event trong lúc chơi.
    // Lọc theo wave nếu có WaveManager.
    // Hai flow: Instant (áp ngay), Choice (đợi người chơi chọn).
    // Bắn event cho UI: OnEventTrigger, OnEventText, OnChoiceEvent, OnEventResolved.
    public class EventManager : MonoBehaviour, IResettable
    {
        [Header("Config")]
        public EventDefinition[] availableEvents;

        [Header("Refs")]
        public GameLoopController gameLoopController;
        [Tooltip("Dùng để lọc event theo wave (unlockAtWave). Nếu trống thì không lọc.")]
        public WaveManager waveManager;

        // [PATCH OBJECTIVE] Áp delta Energy/Stress lên từng agent
        public TaskManager taskManager;

        public event Action<EventDefinition> OnEventTrigger;
        public event Action<string> OnEventText;
        public event Action<ChoiceEventData> OnChoiceEvent;
        public event Action OnEventResolved;

        // ====== Dev QoL (test nhanh) ======
        [Header("Dev Overrides (test)")]
        [SerializeField] bool useOverrideInterval = false;
        [SerializeField, Min(0.1f)] float overrideMin = 3f;
        [SerializeField, Min(0.1f)] float overrideMax = 6f;
        [SerializeField, Tooltip("Giới hạn thời gian chờ Choice (giây). 0 = vô hạn")]
        float choiceTimeout = 20f;

        //reset
        [SerializeField] private Transform runtimeEventRoot; // nếu có

        // ====== Objective Bridge (optional) ======
        [Header("Objective Bridge (optional)")]
        [SerializeField] private WaveObjectiveDef resolveEventsObjective;
        [SerializeField] private bool autoCountResolveEvents = false; // [PATCH OBJECTIVE] tránh double nếu WaveManager cũng đếm

        // ====== Runtime state ======
        float timer;
        float min;
        float max;
        bool isWaitingChoice = false;
        float waitingChoiceTimer = 0f;
        EventDefinition _lastChoiceRoot;

        void Start()
        {
            if (availableEvents == null || availableEvents.Length == 0)
            {
                Debug.LogWarning("[EventManager] Không có Event nào.");
                enabled = false;
                return;
            }

            // Lấy min/max từ list event để random timer
            min = Mathf.Max(0.1f, availableEvents.Min(e => e != null ? e.minIntervalSec : 999f));
            max = Mathf.Max(min + 0.1f, availableEvents.Max(e => e != null ? e.maxIntervalSec : 0.1f));

            // Dev override cho giai đoạn test
            if (useOverrideInterval)
            {
                min = Mathf.Max(0.1f, overrideMin);
                max = Mathf.Max(min + 0.1f, overrideMax);
            }

            if (!gameLoopController) gameLoopController = FindFirstObjectByType<GameLoopController>();
            if (!waveManager) waveManager = FindFirstObjectByType<WaveManager>();
            if (!taskManager) taskManager = FindFirstObjectByType<TaskManager>(); // [PATCH OBJECTIVE]

            ResetTimer();
        }

        void Update()
        {
            // Nếu đang chờ Choice
            if (isWaitingChoice)
            {
                if (choiceTimeout > 0f)
                {
                    // dùng unscaled để vẫn đếm khi UI pause
                    waitingChoiceTimer += Time.unscaledDeltaTime;
                    if (waitingChoiceTimer >= choiceTimeout && _lastChoiceRoot != null)
                    {
                        Debug.LogWarning("[EventManager] Choice timeout → auto chọn A.");
                        ApplyChoice(_lastChoiceRoot, true);
                    }
                }
                return;
            }

            // Đếm timer bình thường
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                TriggerRandomEvent();
                ResetTimer();
            }
        }

        [ContextMenu("Debug/Trigger Now")]
        public void DebugTriggerNow()
        {
            TriggerRandomEvent();
            ResetTimer();
        }

        public void TriggerRandomEvent()
        {
            var pool = availableEvents?.Where(e => e != null).ToArray();
            if (pool == null || pool.Length == 0) return;

            int currentWaveOneBased = waveManager != null ? waveManager.GetCurrentWaveIndex() + 1 : 1;

            var unlockedPool = pool
                .Where(e => Mathf.Max(1, e.unlockAtWave) <= currentWaveOneBased)
                .ToArray();

            if (unlockedPool.Length == 0)
            {
                Debug.LogWarning($"[EventManager] Không có event unlock ở Wave {currentWaveOneBased}. " +
                                 $"Hãy đặt unlockAtWave ≤ {currentWaveOneBased} cho vài event để kiểm tra.");
                return;
            }

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
                waitingChoiceTimer = 0f;
                _lastChoiceRoot = chosen;
                OnChoiceEvent?.Invoke(ChoiceEventData.From(chosen));
            }
        }

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

            // [PATCH OBJECTIVE] Áp delta lên team thật sự
            ApplyTeamDeltas(e.teamEnergyTextDelta, e.teamStressTextDelta);

            // ✅ Đếm vào objective (nếu bật)
            IncResolveEvents(1f);
            OnEventResolved?.Invoke();
        }

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

            // [PATCH OBJECTIVE] Áp delta lên team thật sự
            ApplyTeamDeltas(opt.teamEnergyTextDelta, opt.teamStressTextDelta);

            // ✅ Đếm vào objective (nếu bật)
            IncResolveEvents(1f);
            OnEventResolved?.Invoke();

            isWaitingChoice = false;
            ResetTimer();
        }

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

            // [PATCH OBJECTIVE] Áp delta lên team thật sự
            ApplyTeamDeltas(option.teamEnergyTextDelta, option.teamStressTextDelta);

            // ✅ Đếm vào objective (nếu bật)
            IncResolveEvents(1f);
            OnEventResolved?.Invoke();

            isWaitingChoice = false;
            ResetTimer();
        }

        void ResetTimer() => timer = Random.Range(min, max);

        static string BuildTeamText(int energyDelta, int stressDelta)
        {
            string e = energyDelta == 0 ? "" : (energyDelta > 0 ? $"+{energyDelta} Energy team" : $"{energyDelta} Energy team");
            string s = stressDelta == 0 ? "" : (stressDelta > 0 ? $"+{stressDelta} Stress team" : $"{stressDelta} Stress team");

            if (string.IsNullOrEmpty(e) && string.IsNullOrEmpty(s)) return "";
            if (!string.IsNullOrEmpty(e) && !string.IsNullOrEmpty(s)) return $"{e} • {s}";
            return string.IsNullOrEmpty(e) ? s : e;
        }

        // ===== Objective helper (nội bộ) =====
        private void IncResolveEvents(float amount = 1f)
        {
            if (!autoCountResolveEvents || resolveEventsObjective == null) return;

            resolveEventsObjective.currentValue += amount;

            if (resolveEventsObjective.targetValue > 0 &&
                resolveEventsObjective.currentValue >= resolveEventsObjective.targetValue)
            {
                resolveEventsObjective.currentValue = resolveEventsObjective.targetValue;
                resolveEventsObjective.completed = true;
            }
        }

        // [PATCH OBJECTIVE] Áp delta Energy/Stress lên từng agent + (tùy) cập nhật cực trị cho GLC
        private void ApplyTeamDeltas(int energyDelta, int stressDelta)
        {
            if (taskManager == null || taskManager.ActiveAgents == null) return;

            float maxStress = 0f;
            float minEnergy = float.MaxValue;

            for (int i = 0; i < taskManager.ActiveAgents.Count; i++)
            {
                var a = taskManager.ActiveAgents[i];
                if (a == null) continue;

                var stats = a.GetComponent<CharacterStats>();
                if (stats == null) continue;

                // Energy dương = tăng năng lượng; Stress dương = tăng stress
                stats.ApplyDelta(energyDelta, stressDelta);
                stats.Notify();

                // Thử đọc trị số hiện tại bằng reflection để không phụ thuộc API cụ thể
                float s = ReadFloat(stats, "CurrentStress", "Stress", "stress");
                float e = ReadFloat(stats, "CurrentEnergy", "Energy", "energy");

                if (s > maxStress) maxStress = s;
                if (e < minEnergy) minEnergy = e;
            }

            // (Tùy) cập nhật cực trị lại cho GameLoopController nếu có SetTeamExtremes
            if (gameLoopController != null)
            {
                var mi = gameLoopController.GetType().GetMethod("SetTeamExtremes",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (mi != null)
                {
                    if (minEnergy == float.MaxValue) minEnergy = 0f;
                    try { mi.Invoke(gameLoopController, new object[] { maxStress, minEnergy }); }
                    catch { /* ignore */ }
                }
                else
                {
                    // Không có API → bỏ qua, WaveManager sẽ tự sample lại bằng TaskManager vào frame sau
                }
            }
        }

        private static float ReadFloat(object obj, params string[] names)
        {
            if (obj == null) return 0f;
            var t = obj.GetType();

            // ưu tiên property
            foreach (var n in names)
            {
                var p = t.GetProperty(n, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (p != null && p.PropertyType == typeof(int)) return (int)p.GetValue(obj);
                if (p != null && p.PropertyType == typeof(float)) return (float)p.GetValue(obj);
            }
            // fallback field
            foreach (var n in names)
            {
                var f = t.GetField(n, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (f != null && f.FieldType == typeof(int)) return (int)f.GetValue(obj);
                if (f != null && f.FieldType == typeof(float)) return (float)f.GetValue(obj);
            }
            return 0f;
        }

        public void ResetState()
        {
            StopAllCoroutines();

            var root = runtimeEventRoot != null ? runtimeEventRoot : transform;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                if (child != null) Destroy(child.gameObject);
            }

            // (tuỳ chọn) reset biến đơn giản nếu có
            // nextSpawnTime = 0f;
        }

        // ===== Dữ liệu cho UI Choice =====
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
}
