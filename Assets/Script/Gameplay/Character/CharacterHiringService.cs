using System.Collections.Generic;
using UnityEngine;
using Wargency.UI;

namespace Wargency.Gameplay
{
    // Hệ thống Thuê (Hiring) độc lập với Wave, dùng ngân sách từ BudgetController
    // Khi thuê => trừ tiền => spawn nhân vật => setup => bắn event
    public class CharacterHiringService : MonoBehaviour
    {
        [Header("Prefab & Difficulty")]
        [SerializeField, Tooltip("Prefab fallback có CharacterAgent + CharacterStats + SpriteRenderer")]
        private CharacterAgent agentPrefab; // fallback nếu không tìm được prefab từ Definition/Catalog

        [SerializeField, Tooltip("Optional: kéo WaveManager (implement IDifficultyProvider) để agent mới chịu ảnh hưởng difficulty hiện thời")]
        private UnityEngine.Object difficultyProviderObj;

        [SerializeField, Tooltip("Optional: nếu null sẽ FindAnyObjectByType")]
        private TaskManager taskManager;

        private IDifficultyProvider difficultyProvider;

        [Header("Spawning (fallback kiểu cũ)")]
        [SerializeField, Tooltip("Tập điểm spawn mặc định. Nếu không gán SeatSpawner thì dùng cái này")]
        private SpawnPointSets spawnPointSet;

        [Header("Parent của các Agent sau khi spawn (tùy chọn)")]
        [SerializeField] private Transform agentsParent;

        [Header("UI (optional)")]
        [SerializeField] private UILiveAlertsFeed alertsFeed; // Kéo từ Inspector cho chắc

        // === MỚI: SeatSpawner để đặt nhân vật đúng “chân” trước bàn ===
        [Header("Seat Spawner (ưu tiên)")]
        [SerializeField, Tooltip("Nếu gán, hệ thuê sẽ spawn nhân vật vào các feet-anchor (mỗi bàn 1 chỗ). Nếu null => dùng SpawnPointSet như cũ.")]
        private SeatSpawner seatSpawner;

        [System.Serializable]
        public class HireOption
        {
            public GameObject agentPrefabGO;
            public CharacterDefinition definition;
            [Min(0)] public int hireCost = 1000;
            public int activeLimit = -1;
            public int unlockWaveIndex = -1; // 0-based index (Wave2 => 1)
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

        private void Start()
        {
            // âm thanh click (giữ nguyên như bản cũ nếu anh đang dùng)
            AudioManager.Instance.PlaySE(AUDIO.SE_BUTTONCLICK);
        }

        private UILiveAlertsFeed Feed
        {
            get
            {
                if (alertsFeed == null) alertsFeed = FindObjectOfType<UILiveAlertsFeed>();
                return alertsFeed;
            }
        }

        // =========================================================
        // ====== Helper cho UI: Cost & Quyền thuê (public) ========
        // =========================================================

        // Lấy cost thuê theo đúng logic dùng trong Hire(): ưu tiên CharacterDefinition.HireCost
        // nếu = 0 thì fallback qua field hireCost/cost nếu có.
        public bool TryResolveHireCost(CharacterDefinition def, out int cost)
        {
            cost = 0;
            if (def == null) return false;

            // ưu tiên property từ Definition
            cost = def.HireCost;

            // Nếu property = 0 (hoặc chưa set), thử field trong Definition (nếu team có dùng)
            if (cost == 0)
            {
                var f = def.GetType().GetField("hireCost") ?? def.GetType().GetField("cost");
                if (f != null)
                {
                    object val = f.GetValue(def);
                    if (val is int i) cost = i;
                }
            }

            // Nếu vẫn = 0, thử lấy từ HireOption trong catalog (nếu có)
            if (cost == 0)
            {
                var opt = hireCatalog.Find(o => o.definition == def);
                if (opt != null) cost = Mathf.Max(0, opt.hireCost);
            }

            return cost >= 0;
        }

        /// <summary>
        /// Check đủ tiền dựa trên BudgetController.I.Balance (nguồn sự thật về ngân sách).
        /// </summary>
        public bool CanAfford(CharacterDefinition def)
        {
            if (BudgetController.I == null || def == null) return false;
            return TryResolveHireCost(def, out int cost) && BudgetController.I.Balance >= cost;
        }

        /// <summary>
        /// Check có thể thuê NGAY BÂY GIỜ (đã mở khóa + chưa vượt limit + đủ ngân sách).
        /// </summary>
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

        // =========================================================
        // ================== Logic gốc (giữ nguyên) ===============
        // =========================================================

        // Kiểm tra điều kiện KHÔNG liên quan đến ngân sách
        public bool CanHireNonBudget(CharacterDefinition def, int currentWaveIndex, out HireOption opt, out string reason)
        {
            opt = hireCatalog.Find(o => o.definition == def);

            if (opt == null) { reason = "Không tìm thấy tùy chọn thuê theo kiểu nhân vật này."; return false; }
            if (opt.definition == null) { reason = "HireOption thiếu CharacterDefinition."; return false; }

            if (opt.unlockWaveIndex >= 0 && currentWaveIndex < opt.unlockWaveIndex)
            { reason = $"Chưa mở khóa. Cần WaveIndex >= {opt.unlockWaveIndex}."; return false; }

            if (opt.activeLimit >= 0)
            {
                int count = CountActive(opt.definition);
                if (count >= opt.activeLimit)
                { reason = $"Đã đạt giới hạn nhân sự kiểu này đang chạy: ({count}/{opt.activeLimit})."; return false; }
            }

            reason = null;
            return true;
        }

        // ========== ENTRY POINT thuê nhân sự ==========
        // Ưu tiên SeatSpawner. Nếu không có => fallback về SpawnPointSet như cũ.
        public CharacterAgent Hire(CharacterDefinition def, int currentWaveIndex = int.MaxValue)
        {
            if (seatSpawner != null)
                return HireViaSeatSpawner(def, currentWaveIndex);

            // fallback logic cũ (giữ nguyên API)
            var pos = ResolveSpawnPosition();
            return Hire(def, pos, currentWaveIndex);
        }

        // ========== Thuê theo tọa độ cụ thể (fallback cũ) ==========
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
            // Nếu Definition không có cost, thử lấy từ HireOption (giúp đồng bộ UI/Service)
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

        // ========== PHẦN MỚI: Thuê qua SeatSpawner ==========
        private CharacterAgent HireViaSeatSpawner(CharacterDefinition def, int currentWaveIndex)
        {
            if (def == null) { Debug.LogWarning("[Hiring] Thiếu CharacterDefinition."); return null; }

            // 1) Kiểm tra điều kiện không liên quan ngân sách
            if (!CanHireNonBudget(def, currentWaveIndex, out var opt, out var reason))
            { Debug.LogWarning($"[Hiring] Thuê thất bại: {reason}"); return null; }

            // 2) Check hệ lõi
            var glc = GameLoopController.Instance;
            if (glc == null) { Debug.LogError("[Hiring] GameLoopController.Instance = null."); return null; }
            if (BudgetController.I == null) { Debug.LogError("[Hiring] BudgetController chưa có trong scene!"); Feed?.Push("⚠️ Thiếu BudgetController trong scene"); return null; }

            // 3) Tính chi phí
            int cost = def.HireCost;
            if (cost == 0)
            {
                var f = def?.GetType().GetField("hireCost") ?? def?.GetType().GetField("cost");
                if (f != null) cost = (int)f.GetValue(def);
            }
            if (cost == 0 && opt != null) cost = Mathf.Max(0, opt.hireCost);

            // 4) Trừ ngân sách (nếu không đủ thì dừng)
            if (!BudgetController.I.TrySpend(cost))
            {
                string dn = def != null && !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : (def != null ? def.name : "nhân sự");
                Feed?.Push($"❌ Không đủ ngân sách để thuê {dn}");
                Debug.LogWarning("[Hiring] Không đủ ngân sách để thuê.");
                return null;
            }

            // 5) Chọn prefab để spawn
            var prefabCA = ResolvePrefab(opt, def, agentPrefab);
            if (!prefabCA) { Debug.LogWarning("[Spawn] Không tìm thấy prefab CharacterAgent"); return null; }

            // 6) Lấy Definition để setup
            var resolvedDef = ResolveDefinitionForSpawn(opt, prefabCA);
            if (resolvedDef == null) { Debug.LogWarning("[Hiring] Missing CharacterDefinition."); return null; }

            // 7) Spawn qua SeatSpawner (đứng đúng chân, mỗi bàn 1 người)
            var spawnedGO = seatSpawner.Spawn(prefabCA.gameObject);
            if (spawnedGO == null)
            {
                Debug.LogWarning("[Hiring] SeatSpawner: hết chỗ trống!");

                // ✅ Hoàn tiền đúng API BudgetController hiện tại
                BudgetController.I.Add(cost);

                return null;
            }

            // 8) Setup CharacterAgent
            var agent = spawnedGO.GetComponent<CharacterAgent>();
            if (!agent)
            {
                Debug.LogError("[Hiring] Prefab thiếu CharacterAgent component!");
                return null;
            }
            agent.SetupCharacter(resolvedDef, difficultyProvider, taskManager);

            // 9) Đăng ký HUD
            var wire = FindAnyObjectByType<UIHudWireUp>();
            wire?.Register(agent);

            // 10) Bắn event + lưu active list
            OnAgentHired?.Invoke(agent);
            if (!activeAgents.Contains(agent))
                activeAgents.Add(agent);

            Debug.Log($"[Hiring] Hired via SeatSpawner: {def.DisplayName} ({def.Role})");
            return agent;
        }

        // =========================================================
        // =============== Helpers còn lại (giữ nguyên) ============
        // =========================================================

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
