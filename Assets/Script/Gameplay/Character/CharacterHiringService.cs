using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Wargency.UI;

namespace Wargency.Gameplay
{
    // Hệ thống Thuê (Hiring) độc lập với Wave, dùng ngân sách từ BudgetController
    // Khi thuê => trừ tiền => spawn nhân vật => setup => bắn event
    public class CharacterHiringService : MonoBehaviour
    {
        [Header("Prefab & Difficulty")]
        [SerializeField] private CharacterAgent agentPrefab; // fallback nếu không tìm được prefab từ Definition/Catalog
        [SerializeField] private UnityEngine.Object difficultyProviderObj;
        [SerializeField] private TaskManager taskManager;
        private IDifficultyProvider difficultyProvider;

        [Header("Spawning (fallback kiểu cũ)")]
        [SerializeField] private SpawnPointSets spawnPointSet;

        [Header("Parent của các Agent sau khi spawn (tùy chọn)")]
        [SerializeField] private Transform agentsParent;

        [Header("UI (optional)")]
        [SerializeField] private UILiveAlertsFeed alertsFeed;

        [Header("Seat Spawner (ưu tiên)")]
        [SerializeField] private SeatSpawner seatSpawner;

        [System.Serializable]
        public class HireOption
        {
            public GameObject agentPrefabGO;
            public CharacterDefinition definition;
            [Min(0)] public int hireCost = 1000;
            public int activeLimit = -1;
            public int unlockWaveIndex = -1; // 0-based index
        }

        [Header("Headhunter Catalog")]
        [SerializeField] private List<HireOption> hireCatalog = new();
        private readonly List<CharacterAgent> activeAgents = new();
        public IReadOnlyList<CharacterAgent> ActiveAgents => activeAgents;
        public IReadOnlyList<HireOption> Catalog => hireCatalog;

        [Header("Close Button")]
        [SerializeField] private Button hrCloseButton; // nếu gán bấm để tắt panel
        [SerializeField] private GameObject hrPanelRoot; // panel show/hide

        public event System.Action<CharacterAgent> OnAgentHired;

        private void Awake()
        {
            if (difficultyProviderObj is IDifficultyProvider dp) difficultyProvider = dp;
            if (!taskManager) taskManager = FindAnyObjectByType<TaskManager>();
        }

        private void Start()
        {
            if (hrCloseButton && hrPanelRoot)
            {
                hrCloseButton.onClick.RemoveAllListeners();
                hrCloseButton.onClick.AddListener(() =>
                {
                    hrPanelRoot.SetActive(false);
                    AudioManager.Instance.PlaySE(AUDIO.SE_BUTTONCLICK);
                });
            }
        }

        private UILiveAlertsFeed Feed
        {
            get
            {
                if (alertsFeed == null) alertsFeed = FindObjectOfType<UILiveAlertsFeed>();
                return alertsFeed;
            }
        }

        // Lấy cost thuê theo đúng logic dùng trong Hire()
        public bool TryResolveHireCost(CharacterDefinition def, out int cost)
        {
            cost = 0;
            if (def == null) return false;

            cost = def.HireCost;

            if (cost == 0)
            {
                var f = def.GetType().GetField("hireCost") ?? def.GetType().GetField("cost");
                if (f != null)
                {
                    object val = f.GetValue(def);
                    if (val is int i) cost = i;
                }
            }

            if (cost == 0)
            {
                var opt = hireCatalog.Find(o => o.definition == def);
                if (opt != null) cost = Mathf.Max(0, opt.hireCost);
            }

            return cost >= 0;
        }

        public bool CanAfford(CharacterDefinition def)
        {
            if (BudgetController.I == null || def == null) return false;
            return TryResolveHireCost(def, out int cost) && BudgetController.I.Balance >= cost;
        }

        public bool CanHireNow(CharacterDefinition def, int currentWaveIndex, out string reason)
        {
            reason = null;
            if (!CanHireNonBudget(def, currentWaveIndex, out var _, out var why))
            {
                reason = why;
                return false;
            }

            if (!CanAfford(def))
            {
                if (!TryResolveHireCost(def, out int c)) c = 0;
                string name = def != null && !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : (def ? def.name : "nhân sự");
                reason = $"Cần {c} budget để thuê {name}.";
                return false;
            }

            return true;
        }

        // ================== Logic gốc (giữ nguyên) ===============

        public bool CanHireNonBudget(CharacterDefinition def, int currentWaveIndex, out HireOption opt, out string reason)
        {
            opt = hireCatalog.Find(o => o.definition == def);

            if (opt == null) { reason = "Không tìm thấy tùy chọn thuê."; return false; }
            if (opt.definition == null) { reason = "HireOption thiếu CharacterDefinition."; return false; }

            if (opt.unlockWaveIndex >= 0 && currentWaveIndex < opt.unlockWaveIndex)
            { reason = $"Chưa mở khóa. Cần WaveIndex >= {opt.unlockWaveIndex}."; return false; }

            if (opt.activeLimit >= 0)
            {
                int count = CountActive(opt.definition);
                if (count >= opt.activeLimit)
                { reason = $"Đã đạt giới hạn nhân sự: ({count}/{opt.activeLimit})."; return false; }
            }

            reason = null;
            return true;
        }

        //ENTRY POINT thuê nhân sự 
        public CharacterAgent Hire(CharacterDefinition def, int currentWaveIndex = int.MaxValue)
        {
            if (seatSpawner != null)
                return HireViaSeatSpawner(def, currentWaveIndex);

            var pos = ResolveSpawnPosition();
            return Hire(def, pos, currentWaveIndex);
        }

        public CharacterAgent Hire(CharacterDefinition def, Vector3 worldPos, int currentWaveIndex = int.MaxValue)
        {
            if (def == null) { Debug.LogWarning("[Hiring] Thiếu CharacterDefinition."); return null; }

            if (!CanHireNonBudget(def, currentWaveIndex, out var opt, out var reason))
            { Debug.LogWarning($"[Hiring] Thuê thất bại: {reason}"); return null; }

            var glc = GameLoopController.Instance;
            if (glc == null) { Debug.LogError("[Hiring] GameLoopController.Instance = null."); return null; }
            if (BudgetController.I == null) { Debug.LogError("[Hiring] BudgetController chưa có trong scene!"); Feed?.Push("⚠️ Thiếu BudgetController trong scene"); return null; }

            int cost = def.HireCost;
            if (cost == 0)
            {
                var f = def?.GetType().GetField("hireCost") ?? def?.GetType().GetField("cost");
                if (f != null) cost = (int)f.GetValue(def);
            }
            if (cost == 0 && opt != null) cost = Mathf.Max(0, opt.hireCost);

            if (!BudgetController.I.TrySpend(cost))
            {
                string dn = def != null && !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : (def != null ? def.name : "nhân sự");
                Feed?.Push($"❌ Không đủ ngân sách để thuê {dn}");
                Debug.LogWarning("[Hiring] Không đủ ngân sách để thuê.");
                return null;
            }

            var prefabCA = ResolvePrefab(opt, def, agentPrefab);
            if (!prefabCA) { Debug.LogWarning("[Spawn] Không tìm thấy prefab CharacterAgent"); return null; }

            var resolvedDef = ResolveDefinitionForSpawn(opt, prefabCA);
            if (resolvedDef == null) { Debug.LogWarning("[Hiring] Missing CharacterDefinition."); return null; }

            var parent = GetSpawnParent();
            var agent = Instantiate(prefabCA, worldPos, Quaternion.identity, parent);
            agent.SetupCharacter(resolvedDef, difficultyProvider, taskManager);

            var wire = FindAnyObjectByType<UIHudWireUp>();
            wire?.Register(agent);

            OnAgentHired?.Invoke(agent);

            if (!activeAgents.Contains(agent))
                activeAgents.Add(agent);

            Debug.Log($"[Hiring] Hired: {def.DisplayName} ({def.Role}) tại {worldPos}");
            return agent;
        }

        private CharacterAgent HireViaSeatSpawner(CharacterDefinition def, int currentWaveIndex)
        {
            if (def == null) { Debug.LogWarning("[Hiring] Thiếu CharacterDefinition."); return null; }

            if (!CanHireNonBudget(def, currentWaveIndex, out var opt, out var reason))
            { Debug.LogWarning($"[Hiring] Thuê thất bại: {reason}"); return null; }

            var glc = GameLoopController.Instance;
            if (glc == null) { Debug.LogError("[Hiring] GameLoopController.Instance = null."); return null; }
            if (BudgetController.I == null) { Debug.LogError("[Hiring] BudgetController chưa có trong scene!"); Feed?.Push("Thiếu BudgetController trong scene"); return null; }

            int cost = def.HireCost;
            if (cost == 0)
            {
                var f = def?.GetType().GetField("hireCost") ?? def?.GetType().GetField("cost");
                if (f != null) cost = (int)f.GetValue(def);
            }
            if (cost == 0 && opt != null) cost = Mathf.Max(0, opt.hireCost);

            if (!BudgetController.I.TrySpend(cost))
            {
                string dn = def != null && !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : (def != null ? def.name : "nhân sự");
                Feed?.Push($"❌ Không đủ ngân sách để thuê {dn}");
                Debug.LogWarning("[Hiring] Không đủ ngân sách để thuê.");
                return null;
            }

            var prefabCA = ResolvePrefab(opt, def, agentPrefab);
            if (!prefabCA) { Debug.LogWarning("[Spawn] Không tìm thấy prefab CharacterAgent"); return null; }

            var resolvedDef = ResolveDefinitionForSpawn(opt, prefabCA);
            if (resolvedDef == null) { Debug.LogWarning("[Hiring] Missing CharacterDefinition."); return null; }

            var spawnedGO = seatSpawner.Spawn(prefabCA.gameObject);
            if (spawnedGO == null)
            {
                Debug.LogWarning("[Hiring] SeatSpawner: hết chỗ trống!");
                BudgetController.I.Add(cost);
                return null;
            }

            var agent = spawnedGO.GetComponent<CharacterAgent>();
            if (!agent)
            {
                Debug.LogError("[Hiring] Prefab thiếu CharacterAgent component!");
                return null;
            }
            agent.SetupCharacter(resolvedDef, difficultyProvider, taskManager);

            var wire = FindAnyObjectByType<UIHudWireUp>();
            wire?.Register(agent);

            OnAgentHired?.Invoke(agent);
            if (!activeAgents.Contains(agent))
                activeAgents.Add(agent);

            Debug.Log($"[Hiring] Hired via SeatSpawner: {def.DisplayName} ({def.Role})");
            return agent;
        }

        public void Dismiss(CharacterAgent agent)
        {
            if (!agent) return;
            if (activeAgents.Remove(agent))
            {
                agent.ReleaseTask();
                Destroy(agent.gameObject);
                Debug.Log($"[Hiring] Dismissed: {agent.Definition?.DisplayName ?? agent.name}");
            }
        }

        public int CountActive(CharacterDefinition def)
        {
            int count = 0;
            for (int i = 0; i < activeAgents.Count; i++)
            {
                var a = activeAgents[i];
                if (a && a.Definition == def) count++;
            }
            return count;
        }

        public void SetUnlockWave(CharacterDefinition def, int unlockWaveIndex)
        {
            var opt = hireCatalog.Find(o => o.definition == def);
            if (opt != null) opt.unlockWaveIndex = unlockWaveIndex;
        }

        private CharacterAgent ResolvePrefab(HireOption opt, CharacterDefinition def, CharacterAgent serviceFallback)
        {
            if (opt != null && opt.agentPrefabGO != null)
            {
                var ca = opt.agentPrefabGO.GetComponent<CharacterAgent>();
                if (ca != null) return ca;
                Debug.LogWarning("[Hiring] agentPrefabGO không có CharacterAgent component");
            }

            if (def != null)
            {
                var fromDef = def.GetRandomPrefab();
                if (fromDef != null) return fromDef;
            }

            return serviceFallback;
        }

        private CharacterDefinition ResolveDefinitionForSpawn(HireOption opt, CharacterAgent prefabCA)
        {
            if (prefabCA != null && prefabCA.Definition != null)
                return prefabCA.Definition;

            return opt != null ? opt.definition : null;
        }

        public Vector3 ResolveSpawnPosition(Vector3? explicitPosition = null)
        {
            if (explicitPosition.HasValue) return explicitPosition.Value;
            if (spawnPointSet != null && spawnPointSet.HasAny) return spawnPointSet.GetNextRoundPoint();
            return transform.position;
        }

        private Transform GetSpawnParent()
        {
            return agentsParent ? agentsParent : transform;
        }

        public void CloseHRPanel()
        {
            if (hrPanelRoot != null)
            {
                hrPanelRoot.SetActive(false);
            }
            else
            {
                Debug.LogWarning("[CharacterHiringService] hrPanelRoot chưa gán — không thể Close.");
            }

            try
            {
                AudioManager.Instance?.PlaySE(AUDIO.SE_BUTTONCLICK);
            }
            catch { }
        }
    }
}
