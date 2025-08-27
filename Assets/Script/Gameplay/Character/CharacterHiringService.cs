using System.Collections.Generic;
using UnityEngine;

namespace Wargency.Gameplay
{
    // Hệ thống Thuê (Hiring) độc lập với Wave, dùng ngân sách từ instance của GameLoopController
    // - TrySpendBudget(amount) sẽ tự trừ tiền nếu đủ.
    // - Kiểm tra limit & unlock wave trước; ngân sách check ngay khi thuê.

    public class CharacterHiringService : MonoBehaviour
    {
        [Header("Prefab & Difficulty")]
        [SerializeField, Tooltip("Prefab fallback có CharacterAgent + CharacterStats + SpriteRenderer")]
        private CharacterAgent agentPrefab; //Chỉ Fallback khi Definition trống

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

        [System.Serializable]
        public class HireOption
        {
            [Tooltip("Prefab nhân vật (GameObject có CharacterAgent). ƯU TIÊN cao nhất")]
            public GameObject agentPrefabGO;

            [Tooltip("SO nhân vật (backup nếu prefab không tự khai Definition)")]
            public CharacterDefinition definition;

            [Min(0), Tooltip("Chi phí thuê")]
            public int hireCost = 1000;

            [Tooltip("Giới hạn số agent loại này đang active (-1 = không giới hạn)")]
            public int activeLimit = -1;

            [Tooltip("Mở khóa theo progression (vd Wave >= x). -1 = luôn mở")]
            public int unlockWaveIndex = -1;
        }

        [Header("Headhunter Catalog")]
        [SerializeField] private List<HireOption> hireCatalog = new();
        private readonly List<CharacterAgent> activeAgents = new();
        public IReadOnlyList<CharacterAgent> ActiveAgents => activeAgents;
        public IReadOnlyList<HireOption> Catalog => hireCatalog;

        public event System.Action<CharacterAgent> OnAgentHired;

        private void Awake()
        {
            if (difficultyProviderObj is IDifficultyProvider difficultprovide) difficultyProvider = difficultprovide;
            if (!taskManager) taskManager = FindAnyObjectByType<TaskManager>();
        }


        // Kiểm tra điều kiện KHÔNG liên quan đến ngân sách (limit/unlock). Dành cho UI check thôi
        // Ngân sách sẽ kiểm tra khi gọi Hire (vì TrySpendBudget trừ tiền khi thành công).
        public bool CanHireNonBudget(CharacterDefinition def, int currentWaveIndex, out HireOption opt, out string reason)
        {
            opt = hireCatalog.Find(o => o.definition == def);

            if (opt == null) { reason = "Không tìm thấy tùy chọn thuê theo kiểu nhân vật này."; return false; }
            if (opt.definition == null) { reason = "HireOption thiếu CharacterDefinition."; return false; }

            if (opt.unlockWaveIndex >= 0 && currentWaveIndex < opt.unlockWaveIndex)
            {
                reason = $"Chưa mở khóa. Cần Wave >= {opt.unlockWaveIndex}.";
                return false;
            }

            if (opt.activeLimit >= 0)
            {
                int count = CountActive(opt.definition);
                if (count >= opt.activeLimit)
                {
                    reason = $"Đã đạt giới hạn nhân sự kiểu này đang chạy: ({count}/{opt.activeLimit}).";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        // Overload: Không truyền vị trí → tự lấy từ SpawnPointSets (round-robin) hoặc transform.position
        public CharacterAgent Hire(CharacterDefinition def, int currentWaveIndex = int.MaxValue)
        {
            var pos = ResolveSpawnPosition(); // tự xử lý round-robin nếu có spawnPointSet
            return Hire(def, pos, currentWaveIndex);
        }
        // Thuê & spawn tại worldPos.
        // - Trả null nếu: không qua điều kiện unlock/limit, hoặc ngân sách không đủ (TrySpendBudget trả false).
        // - Lưu ý: TrySpendBudget sẽ TỰ trừ tiền nếu đủ.
        public CharacterAgent Hire(CharacterDefinition def, Vector3 worldPos, int currentWaveIndex = int.MaxValue)
        {
            if (def == null) { Debug.LogWarning("[Hiring] Thiếu CharacterDefinition."); return null; }

            if (!CanHireNonBudget(def, currentWaveIndex, out var opt, out var reason))
            {
                Debug.LogWarning($"[Hiring] Thuê thất bại: {reason}");
                return null;
            }
            // Lấy singleton GameLoopController
            var glc = GameLoopController.Instance;
            if (glc == null)
            {
                Debug.LogError("[Hiring] GameLoopController.Instance = null. Hãy đảm bảo GameLoopController đã có trong scene và khởi tạo Instance ở Awake.");
                return null;
            }

            // Check & trừ ngân sách: TrySpendBudget sẽ trừ tiền khi thành công
            if (!glc.TrySpendBudget(opt.hireCost))
            {
                Debug.LogWarning($"[Hiring] Không đủ ngân sách để thuê (cần {opt.hireCost}).");
                return null;
            }

            // Spawn & setup
            var prefabCA = ResolvePrefab(opt, def, agentPrefab);
            if (!prefabCA) { Debug.LogWarning("[Spawn] không tìm thấy prefab"); return null; }

            // Nếu ở trên không có: resolve Definition cuối cùng => chuyển qua ưu tiên definition gắn trên prefab
            var resolvedDef = ResolveDefinitionForSpawn(opt, prefabCA);
            if (resolvedDef == null)
            {
                Debug.LogWarning("[Hiring] Missing CharacterDefinition (option null & prefab không gán).");
                return null;
            }

            // Parent giữ là transform hiện tại (quản lý theo service)
            var parent = GetSpawnParent();
            var agent = Instantiate(prefabCA, worldPos, Quaternion.identity, parent);
            agent.SetupCharacter(resolvedDef, difficultyProvider, taskManager);

            //2408 - đăng ký có agent
            RegisterIntoTaskManager(agent);
            //2708 - báo cho UIHudWireUp
            OnAgentHired?.Invoke(agent);
            //Track Active list để activeLimit hoạt động chính xác
            if (!activeAgents.Contains(agent))
                activeAgents.Add(agent);
            Debug.Log($"[Hiring] Hired: {def.DisplayName} ({def.Role}) với giá {opt.hireCost} tại {worldPos}");
            return agent;
        }
       

        // Sa thải agent: release task và hủy game Object
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

        // Đếm số agent đang active theo definition
        public int CountActive(CharacterDefinition def)
        {
            int count = 0;
            for (int i = 0; i < activeAgents.Count; i++)
            {
                var a = activeAgents[i];
                if (a && a.Definition == def)
                    count++;
            }
            return count;
        }

        // cập nhật yêu cầu mở khóa theo progression (nếu muốn thay đổi runtime)
        public void SetUnlockWave(CharacterDefinition def, int unlockWaveIndex)
        {
            var opt = hireCatalog.Find(o => o.definition == def);
            if (opt != null) opt.unlockWaveIndex = unlockWaveIndex;
        }


        //----------- Helper của đống trên ----------------------

        // Update ngày 2408: đăng ký active agent
        private void RegisterIntoTaskManager(CharacterAgent agent)
        {
            if (agent == null) return;
            if (taskManager == null) taskManager = FindAnyObjectByType<TaskManager>(); // phòng quên kéo
            if (taskManager != null)
            {
                taskManager.RegisterAgent(agent);
                Debug.Log($"[Hiring] Registered to TaskManager: {agent.name} ({agent.Definition?.Role})");
            }
            else
            {
                Debug.LogWarning("[Hiring] Không tìm thấy TaskManager để RegisterAgent. UI/AssignTask sẽ không thấy agent này!");
            }
        }

        // Ưu tiên prefab từ Definition; nếu rỗng => fallback mặc định của service
        private CharacterAgent ResolvePrefab(HireOption opt, CharacterDefinition def, CharacterAgent serviceFallback)
        {
            // 1) Prefab từ Option (GameObject có CharacterAgent)
            if (opt != null && opt.agentPrefabGO != null)
            {
                var ca = opt.agentPrefabGO.GetComponent<CharacterAgent>();
                if (ca != null) return ca;
                Debug.LogWarning("[Hiring] agentPrefabGO không có CharacterAgent component");
            }

            // 2) Prefab từ Definition (random variant)
            if (def != null)
            {
                var fromDef = def.GetRandomPrefab();
                if (fromDef != null) return fromDef;
            }

            // 3) Fallback của Service
            return serviceFallback;
        }

        //Lấy definition cuối cùng cho SetupCharacter
        private CharacterDefinition ResolveDefinitionForSpawn(HireOption opt, CharacterAgent prefabCA)
        {
            // Nếu prefab đã gán sẵn definition → ưu tiên
            if (prefabCA != null && prefabCA.Definition != null)
                return prefabCA.Definition;

            // Ngược lại dùng definition trong option
            return opt != null ? opt.definition : null;
        }

        public Vector3 ResolveSpawnPosition(Vector3? explicitPosition = null)
        {
            if (explicitPosition.HasValue)
                return explicitPosition.Value;

            if (spawnPointSet != null && spawnPointSet.HasAny)
                return spawnPointSet.GetNextRoundPoint(); // gọi API mới

            return transform.position;
        }

        private Transform GetSpawnParent()
        {
            return agentsParent? agentsParent : transform; // fallback về object đang gắn service
        }
    }
}
