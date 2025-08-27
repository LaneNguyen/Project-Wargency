using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wargency.Gameplay
{
    // Random Task Cycle (gacha)
    // - Mỗi khoảng thời gian ngẫu nhiên sẽ thử spawn 1 task theo trọng số.
    // - Không vượt TaskManager.maxConcurrentTasks.
    // - Nếu TaskDefinition yêu cầu role:
    //    + Và TaskManager.fallbackMode == Fail -> chỉ spawn khi có ít nhất 1 agent đúng role đang active.
    //    + Và TaskManager.fallbackMode == AnyAgent -> vẫn spawn (TaskManager sẽ tự fallback khi assign).

    [DisallowMultipleComponent]
    public class TaskRandomSpawner : MonoBehaviour
    {
        [Serializable]
        public class WeightedDef
        {
            public TaskDefinition def;
            [Range(0, 100)] public int weight = 10;
        }

        [Header("Run")]
        [SerializeField] private bool autoRun = true;

        [Tooltip("Khoảng thời gian giữa 2 lần thử spawn (giây)")]
        [SerializeField] private Vector2 intervalRangeSec = new Vector2(8f, 15f);

        [Header("Pool (Gacha)")]
        [SerializeField] private List<WeightedDef> pool = new();

        [Header("Refs")]
        [SerializeField] private TaskManager taskManager;

        private float _nextAt;

        private void Awake()
        {
            if (taskManager == null)
                taskManager = FindObjectOfType<TaskManager>();
        }

        private void OnEnable()
        {
            ScheduleNext();
        }

        private void Update()
        {
            if (!autoRun || taskManager == null || pool == null || pool.Count == 0)
                return;

            if (Time.time >= _nextAt)
            {
                TrySpawnOnce();
                ScheduleNext();
            }
        }

        private void ScheduleNext()
        {
            float min = Mathf.Max(0.1f, intervalRangeSec.x);
            float max = Mathf.Max(min, intervalRangeSec.y);
            _nextAt = Time.time + UnityEngine.Random.Range(min, max);
        }

        private void TrySpawnOnce()
        {
            // 1) Tôn trọng giới hạn đang hoạt động
            if (taskManager.ActiveCount >= taskManager.MaxConcurrentTasks)
            {
                Debug.Log($"[Spawner] Skip: reached maxConcurrentTasks ({taskManager.ActiveCount}/{taskManager.MaxConcurrentTasks}).");
                return;
            }

            // 2) Chọn 1 TaskDefinition theo trọng số
            var def = WeightedPick(pool);
            if (def == null)
            {
                Debug.LogWarning("[Spawner] Pool rỗng hoặc total weight = 0.");
                return;
            }

            // 3) Nếu task yêu cầu role và fallback toàn cục = Fail:
            //    -> chỉ spawn khi có ít nhất 1 agent đúng role đang active (tránh task không thể làm).
            if (def.UseRequiredRole && taskManager.fallbackMode == AssignmentFallback.Fail)
            {
                if (!taskManager.HasActiveAgentWithRole(def.RequiredRole))
                {
                    Debug.Log($"[Spawner] Skip '{def.DisplayName}': require {def.RequiredRole} but none active (fallback=Fail).");
                    return;
                }
            }

            // 4) Nhờ TaskManager assign (TaskManager sẽ tự xử lý fallback nếu AnyAgent)
            if (taskManager.AssignTask(def, out var inst))
            {
                Debug.Log($"[Spawner] Spawn OK: '{inst.DisplayName}'.");
            }
            else
            {
                Debug.Log($"[Spawner] Spawn FAIL for '{def.DisplayName}' (hết slot hoặc rule khác).");
            }
        }

        private TaskDefinition WeightedPick(List<WeightedDef> list)
        {
            int total = 0;
            for (int i = 0; i < list.Count; i++)
                total += (list[i].def != null) ? Mathf.Max(0, list[i].weight) : 0;

            if (total <= 0) return null;

            int r = UnityEngine.Random.Range(0, total);
            int acc = 0;
            for (int i = 0; i < list.Count; i++)
            {
                int w = (list[i].def != null) ? Mathf.Max(0, list[i].weight) : 0;
                if (r < acc + w) return list[i].def;
                acc += w;
            }
            return null;
        }

        // ===== Public helpers (tuỳ chọn) =====
        public void SetAutoRun(bool on) => autoRun = on;
        public void SetInterval(float min, float max) => intervalRangeSec = new Vector2(min, max);
        public void SetTaskManager(TaskManager tm) => taskManager = tm;
        public List<WeightedDef> Pool => pool;
        public Vector2 IntervalRangeSec { get => intervalRangeSec; set => intervalRangeSec = value; }
        public bool AutoRun => autoRun;
    }
}
