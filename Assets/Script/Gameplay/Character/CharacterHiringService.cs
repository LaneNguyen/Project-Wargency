using System.Collections.Generic;
using UnityEngine;
using Wargency.UI;

namespace Wargency.Gameplay
{
    // Hệ thống Thuê (Hiring) độc lập với Wave, dùng ngân sách từ instance của GameLoopController
    // Hệ thống thuê nhân sự => trừ tiền xong thì spawn nhân vật tại điểm spawn
    // Có danh bạ nhiều lựa chọn hire để mở khoá theo wave
    // Nhớ kéo BudgetController và điểm spawn cho đủ
    public class CharacterHiringService : MonoBehaviour
    {
        [Header("Prefab & Difficulty")]
        [SerializeField, Tooltip("Prefab fallback có CharacterAgent + CharacterStats + SpriteRenderer")]
        private CharacterAgent agentPrefab; // fallback

        [SerializeField, Tooltip("Optional: kéo WaveManager (implement IDifficultyProvider) để agent mới chịu ảnh hưởng difficulty hiện thời")]
        private UnityEngine.Object difficultyProviderObj;

        [SerializeField, Tooltip("Optional: nếu null sẽ FindAnyObjectByType")]
        private TaskManager taskManager;

        private IDifficultyProvider difficultyProvider;

        [Header("Spawning Points")]
        [SerializeField, Tooltip("Tập điểm spawn mặc định. Nếu null/empty sẽ fallback transform.position")]
        private SpawnPointSets spawnPointSet;

        [Header("Object parent khi thuê character")]
        [SerializeField] private Transform agentsParent;

        // Panel cảnh báo
        [Header("UI (optional)")]
        [SerializeField] private UILiveAlertsFeed alertsFeed; // Kéo từ Inspector cho chắc

        [System.Serializable]
        public class HireOption
        {
            public GameObject agentPrefabGO;
            public CharacterDefinition definition;
            [Min(0)] public int hireCost = 1000;
            public int activeLimit = -1;
            public int unlockWaveIndex = -1;
        }

        [Header("Headhunter Catalog")]
        [SerializeField] private List<HireOption> hireCatalog = new();
        private readonly List<CharacterAgent> activeAgents = new();
        public IReadOnlyList<CharacterAgent> ActiveAgents => activeAgents;
        public IReadOnlyList<HireOption> Catalog => hireCatalog;

        // Sự kiện DUY NHẤT để thông báo cho hệ khác (WaveManager, UI, analytics...)
        public event System.Action<CharacterAgent> OnAgentHired;

        private void Awake()
        {
            if (difficultyProviderObj is IDifficultyProvider dp) difficultyProvider = dp;
            if (!taskManager) taskManager = FindAnyObjectByType<TaskManager>();
        }

        private UILiveAlertsFeed Feed
        {
            get
            {
                if (alertsFeed == null) alertsFeed = FindObjectOfType<UILiveAlertsFeed>();
                return alertsFeed;
            }
        }

        // Kiểm tra điều kiện KHÔNG liên quan đến ngân sách
        public bool CanHireNonBudget(CharacterDefinition def, int currentWaveIndex, out HireOption opt, out string reason)
        {
            opt = hireCatalog.Find(o => o.definition == def);

            if (opt == null) { reason = "Không tìm thấy tùy chọn thuê theo kiểu nhân vật này."; return false; }
            if (opt.definition == null) { reason = "HireOption thiếu CharacterDefinition."; return false; }

            if (opt.unlockWaveIndex >= 0 && currentWaveIndex < opt.unlockWaveIndex)
            { reason = $"Chưa mở khóa. Cần Wave >= {opt.unlockWaveIndex}."; return false; }

            if (opt.activeLimit >= 0)
            {
                int count = CountActive(opt.definition);
                if (count >= opt.activeLimit)
                { reason = $"Đã đạt giới hạn nhân sự kiểu này đang chạy: ({count}/{opt.activeLimit})."; return false; }
            }

            reason = null;
            return true;
        }

        // Overload chọn vị trí spawn rồi chuyển sang overload chính
        public CharacterAgent Hire(CharacterDefinition def, int currentWaveIndex = int.MaxValue)
        {
            var pos = ResolveSpawnPosition();
            return Hire(def, pos, currentWaveIndex);
        }

        // Overload chính: trừ ngân sách, spawn, setup, bắn event
        public CharacterAgent Hire(CharacterDefinition def, Vector3 worldPos, int currentWaveIndex = int.MaxValue)
        {
            if (def == null) { Debug.LogWarning("[Hiring] Thiếu CharacterDefinition."); return null; }

            if (!CanHireNonBudget(def, currentWaveIndex, out var opt, out var reason))
            { Debug.LogWarning($"[Hiring] Thuê thất bại: {reason}"); return null; }

            var glc = GameLoopController.Instance;
            if (glc == null) { Debug.LogError("[Hiring] GameLoopController.Instance = null."); return null; }
            if (BudgetController.I == null) { Debug.LogError("[Hiring] BudgetController chưa có trong scene!"); Feed?.Push("⚠️ Thiếu BudgetController trong scene"); return null; }

            // Lấy cost từ CharacterDefinition (ưu tiên property), fallback qua field
            int cost = def.HireCost;
            if (cost == 0)
            {
                var f = def?.GetType().GetField("hireCost") ?? def?.GetType().GetField("cost");
                if (f != null) cost = (int)f.GetValue(def);
            }

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

            // ✅ HUD để UIHudWireUp tự xử lý, tránh trùng
            var wire = FindAnyObjectByType<UIHudWireUp>();
            wire?.Register(agent);

            // ✅ Chỉ bắn event — không cộng objective ở đây
            OnAgentHired?.Invoke(agent);

            if (!activeAgents.Contains(agent))
                activeAgents.Add(agent);

            Debug.Log($"[Hiring] Hired: {def.DisplayName} ({def.Role}) tại {worldPos}");
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
    }
}
