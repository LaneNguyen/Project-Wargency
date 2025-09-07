using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wargency.Gameplay
{
    // spawner random theo trọng số, giờ có lọc theo wave
    [DisallowMultipleComponent]
    public class TaskRandomSpawner : MonoBehaviour
    {
        [Serializable]
        public class WeightedDef
        {
            public TaskDefinition def;
            [Range(0, 100)] public int weight = 10;
        }

        [Serializable]
        public class WavePool
        {
            public int wave; // wave số mấy
            public List<WeightedDef> pool = new(); // danh sách task cho wave này
        }

        [Header("Run")]
        [SerializeField] private bool autoRun = true;

        [Tooltip("khoảng thời gian giữa 2 lần thử spawn (giây)")]
        [SerializeField] private Vector2 intervalRangeSec = new Vector2(8f, 15f);

        [Header("Pool Settings")]
        [SerializeField] private bool useEqualChance = false; // nếu bật thì bỏ weight, chia đều
        [SerializeField] private List<WavePool> wavePools = new(); // pool riêng cho từng wave
        [SerializeField] private List<WeightedDef> globalPool = new(); // pool chung mọi wave

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
            if (!autoRun || taskManager == null)
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
            if (taskManager.ActiveCount >= taskManager.MaxConcurrentTasks)
            {
                Debug.Log($"[Spawner] Skip: reached maxConcurrentTasks ({taskManager.ActiveCount}/{taskManager.MaxConcurrentTasks}).");
                return;
            }

            int currentWave = (taskManager && taskManager.gameLoopController) ? taskManager.gameLoopController.Wave : 1;

            if (useEqualChance)
            {
                // beginner comment: chia đều thì chỉ cần danh sách task duy nhất thôi
                var equalPool = BuildEqualPoolCumulative(currentWave);
                if (equalPool.Count == 0)
                {
                    Debug.Log($"[Spawner] Pool (equal) không có task hợp lệ cho wave {currentWave}.");
                    return;
                }

                TaskDefinition defEq = EqualPick(equalPool);
                if (defEq == null) return;

                var instEq = taskManager.Spawn(defEq);
                if (instEq != null)
                    Debug.Log($"[Spawner] Spawn OK (Equal): '{instEq.DisplayName}' => chờ người chơi kéo agent + Start All.");
                else
                    Debug.Log($"[Spawner] Spawn FAIL for '{defEq.DisplayName}'.");
            }
            else
            {
                // beginner comment: gacha theo trọng số, nhưng giờ gom các wave <= currentWave nha
                var weighted = BuildWeightedPoolCumulative(currentWave);
                var def = WeightedPick(weighted);
                if (def == null)
                {
                    Debug.Log($"[Spawner] Pool (weighted) không có task hợp lệ cho wave {currentWave}.");
                    return;
                }

                var inst = taskManager.Spawn(def);
                if (inst != null)
                    Debug.Log($"[Spawner] Spawn OK (Weighted): '{inst.DisplayName}' => chờ người chơi kéo agent + Start All.");
                else
                    Debug.Log($"[Spawner] Spawn FAIL for '{def.DisplayName}'.");
            }
        }

        // ===== WEIGHTED MODE (cộng dồn pool các wave <= currentWave) =====
        private List<WeightedDef> BuildWeightedPoolCumulative(int wave)
        {
            // beginner comment: mình gom weight theo task để không bị trùng cộng lần
            var map = new Dictionary<TaskDefinition, int>();

            // global
            for (int i = 0; i < globalPool.Count; i++)
            {
                var it = globalPool[i];
                if (it != null && it.def != null && it.weight > 0 && it.def.IsAvailableAtWave(wave)
                    // UPDATE 2025-09-05: lọc theo role hiện có trong đội để tránh spawn task mồ côi
                    && (!it.def.UseRequiredRole ||
                        (taskManager != null && taskManager.HasActiveAgentWithRole(it.def.RequiredRole))))
                {
                    map[it.def] = (map.TryGetValue(it.def, out var w) ? w : 0) + Mathf.Max(0, it.weight);
                }
            }

            // tất cả wave pool có wp.wave <= currentWave
            for (int wi = 0; wi < wavePools.Count; wi++)
            {
                var wp = wavePools[wi];
                if (wp != null && wp.pool != null && wp.wave <= wave)
                {
                    for (int i = 0; i < wp.pool.Count; i++)
                    {
                        var it = wp.pool[i];
                        if (it != null && it.def != null && it.weight > 0 && it.def.IsAvailableAtWave(wave)
                            // UPDATE 2025-09-05: lọc theo role hiện có trong đội để tránh spawn task mồ côi
                            && (!it.def.UseRequiredRole ||
                                (taskManager != null && taskManager.HasActiveAgentWithRole(it.def.RequiredRole))))
                        {
                            map[it.def] = (map.TryGetValue(it.def, out var w) ? w : 0) + Mathf.Max(0, it.weight);
                        }
                    }
                }
            }

            // xuất ra list WeightedDef
            var list = new List<WeightedDef>(map.Count);
            foreach (var kv in map)
            {
                list.Add(new WeightedDef { def = kv.Key, weight = kv.Value });
            }
            return list;
        }

        private TaskDefinition WeightedPick(List<WeightedDef> list)
        {
            // beginner comment: random theo trọng số, giống gacha đó
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

        // ===== EQUAL MODE (cộng dồn pool các wave <= currentWave) =====
        private List<TaskDefinition> BuildEqualPoolCumulative(int wave)
        {
            // beginner comment: random đều thì chỉ cần 1 lần mỗi task là đủ
            var set = new HashSet<TaskDefinition>();
            var result = new List<TaskDefinition>();

            // global
            for (int i = 0; i < globalPool.Count; i++)
            {
                var it = globalPool[i];
                if (it != null && it.def != null && it.def.IsAvailableAtWave(wave)
                    // UPDATE 2025-09-05: lọc theo role hiện có trong đội để tránh spawn task mồ côi
                    && (!it.def.UseRequiredRole ||
                        (taskManager != null && taskManager.HasActiveAgentWithRole(it.def.RequiredRole))))
                {
                    if (set.Add(it.def)) result.Add(it.def);
                }
            }

            // tất cả wave pool có wp.wave <= currentWave
            for (int wi = 0; wi < wavePools.Count; wi++)
            {
                var wp = wavePools[wi];
                if (wp != null && wp.pool != null && wp.wave <= wave)
                {
                    for (int i = 0; i < wp.pool.Count; i++)
                    {
                        var it = wp.pool[i];
                        if (it != null && it.def != null && it.def.IsAvailableAtWave(wave)
                            // UPDATE 2025-09-05: lọc theo role hiện có trong đội để tránh spawn task mồ côi
                            && (!it.def.UseRequiredRole ||
                                (taskManager != null && taskManager.HasActiveAgentWithRole(it.def.RequiredRole))))
                        {
                            if (set.Add(it.def)) result.Add(it.def);
                        }
                    }
                }
            }

            return result;
        }

        private TaskDefinition EqualPick(List<TaskDefinition> defs)
        {
            // beginner comment: cái này random đều nhau, mỗi task 1 suất => công bằng
            int idx = UnityEngine.Random.Range(0, defs.Count);
            return defs[idx];
        }

        // ===== public setters =====
        public void SetAutoRun(bool on) => autoRun = on;
        public void SetInterval(float min, float max) => intervalRangeSec = new Vector2(min, max);
        public void SetTaskManager(TaskManager tm) => taskManager = tm;

        // (tùy bạn có cần expose thêm không)
        public Vector2 IntervalRangeSec { get => intervalRangeSec; set => intervalRangeSec = value; }
        public bool AutoRun => autoRun;
    }
}
