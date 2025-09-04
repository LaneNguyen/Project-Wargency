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

            // beginner comment: mình ghép global pool + pool riêng của wave hiện tại lại chung nha
            var filtered = BuildWaveFilteredPool(currentWave);

            if (filtered.Count == 0)
            {
                Debug.Log($"[Spawner] Pool không có task hợp lệ cho wave {currentWave}.");
                return;
            }

            TaskDefinition def = useEqualChance ? EqualPick(filtered) : WeightedPick(filtered);
            if (def == null)
                return;

            var inst = taskManager.Spawn(def);
            if (inst != null)
            {
                Debug.Log($"[Spawner] Spawn OK (New): '{inst.DisplayName}' => chờ người chơi kéo agent + Start All.");
            }
            else
            {
                Debug.Log($"[Spawner] Spawn FAIL for '{def.DisplayName}'.");
            }
        }

        private List<WeightedDef> BuildWaveFilteredPool(int wave)
        {
            var list = new List<WeightedDef>();

            // lấy global pool
            foreach (var it in globalPool)
            {
                if (it != null && it.def != null && it.weight > 0 && it.def.IsAvailableAtWave(wave))
                    list.Add(it);
            }

            // lấy pool của wave
            foreach (var wp in wavePools)
            {
                if (wp.wave == wave)
                {
                    foreach (var it in wp.pool)
                    {
                        if (it != null && it.def != null && it.weight > 0 && it.def.IsAvailableAtWave(wave))
                            list.Add(it);
                    }
                }
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

        private TaskDefinition EqualPick(List<WeightedDef> list)
        {
            //cái này random đều nhau, không quan tâm weight
            int r = UnityEngine.Random.Range(0, list.Count);
            return list[r].def;
        }

        public void SetAutoRun(bool on) => autoRun = on;
        public void SetInterval(float min, float max) => intervalRangeSec = new Vector2(min, max);
        public void SetTaskManager(TaskManager tm) => taskManager = tm;
    }
}
