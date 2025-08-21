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
        [SerializeField, Tooltip("Prefab có CharacterAgent + CharacterStats + SpriteRenderer")]
        private CharacterAgent agentPrefab;

        [SerializeField, Tooltip("Optional: kéo WaveManager (implement IDifficultyProvider) để agent mới chịu ảnh hưởng difficulty hiện thời")]
        private UnityEngine.Object difficultyProviderObj;

        [SerializeField, Tooltip("Optional: nếu null sẽ FindAnyObjectByType")]
        private TaskManager taskManager;

        private IDifficultyProvider difficultyProvider;

        [System.Serializable]
        public class HireOption
        {
            [Tooltip("SO nhân vật")]
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

        // Thuê & spawn tại worldPos.
        // - Trả null nếu: không qua điều kiện unlock/limit, hoặc ngân sách không đủ (TrySpendBudget trả false).
        // - Lưu ý: TrySpendBudget sẽ TỰ trừ tiền nếu đủ.

        public CharacterAgent Hire(CharacterDefinition def, Vector3 worldPos, int currentWaveIndex = int.MaxValue)
        {
            if (!agentPrefab || def == null) { Debug.LogWarning("[Hiring] Thiếu prefab/definition."); return null; }

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
            var agent = Instantiate(agentPrefab, worldPos, Quaternion.identity, transform);
            agent.SetupCharacter(def, difficultyProvider, taskManager);
            activeAgents.Add(agent);

            Debug.Log($"[Hiring] Hired: {def.DisplayName} ({def.RoleTag}) với giá {opt.hireCost} tại {worldPos}");
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
    }
}
