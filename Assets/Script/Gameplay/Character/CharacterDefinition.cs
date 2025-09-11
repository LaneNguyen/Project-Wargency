using UnityEngine;
using System.Collections.Generic;

namespace Wargency.Gameplay
{
    // Enum cố định cho Role (ổn định chỉ số để an toàn serialize)
    // file này là hồ sơ nhân vật nè => chứa avatar, body, role, chỉ số cơ bản, prefab để spawn
    // dùng để hiện bên UI thuê với giá tiền và mô tả nho nhỏ
    // đừng đổi tên public api kẻo prefab đang liên kết bị xỉu nhẹ
    public enum CharacterRole { Designer = 0, Copywriter = 1, Planner = 2, Account = 3 }

    [CreateAssetMenu(fileName = "CharacterDefinition", menuName = "Wargency/Character")]
    public class CharacterDefinition : ScriptableObject
    {
        [Header("Định danh")]
        [SerializeField, Tooltip("Unique id, dùng nội bộ")]
        private string id = "char_001";

        [SerializeField, Tooltip("Tên hiển thị")]
        private string displayName = "Designer";

        [Header("Visual nhân vật")]
        [SerializeField, Tooltip("Ảnh chân dung cho UI/Avatar")]
        private Sprite avatar;

        [SerializeField, Tooltip("Sprite body in-scene")]
        private Sprite body;

        [Header("Role (enum)")]
        [SerializeField]
        private CharacterRole role = CharacterRole.Designer;

        [Header("Chỉ số cơ bản")]
        [Min(0)][SerializeField] private int baseEnergy = 100;
        [Min(0)][SerializeField] private int baseStress = 0;
        [Min(0f)][SerializeField] private float moveSpeed = 3.5f;

        [Header("Economy (optional)")]
        [Min(0)]
        [SerializeField, Tooltip("Chi phí thuê (tuỳ hệ thống dùng)")]
        public int HireCost = 100;

        [Header("Mô tả nhân vật")]
        [TextArea(2, 5)]
        [SerializeField, Tooltip("Mô tả ngắn về nhân vật để hiện ở UI thuê, hồ sơ, v.v.")]
        private string description = "Một nhà thiết kế trẻ trung, giàu ý tưởng và yêu pastel.";

        [Header("Prefabs (variants)")]
        [Tooltip("Nếu trống, upstream (Hiring/Wave) sẽ dùng fallback prefab theo ưu tiên: HireOption.agentPrefabGO → Definition.AgentPrefabs → HiringService.agentPrefab.")]
        [SerializeField]
        private List<CharacterAgent> agentPrefabs = new();

        // ===== Public API (giữ tên theo yêu cầu) =====
        public string Id => id;
        public string DisplayName => displayName;
        public Sprite Avatar => avatar;
        public Sprite Body => body;
        public CharacterRole Role => role;
        public int BaseEnergy => baseEnergy;
        public int BaseStress => baseStress;
        public float MoveSpeed => moveSpeed;
        public string Description => description; 
        public IReadOnlyList<CharacterAgent> AgentPrefabs => agentPrefabs;

        // Có prefab không?
        public bool HasPrefabs => agentPrefabs != null && agentPrefabs.Count > 0;

        // Lấy prefab ngẫu nhiên
        public CharacterAgent GetRandomPrefab()
        {
            if (!HasPrefabs) return null;
            int i = Random.Range(0, agentPrefabs.Count);
            return agentPrefabs[i];
        }

        // Lấy prefab theo index an toàn
        public CharacterAgent GetPrefabByIndex(int index)
        {
            if (!HasPrefabs) return null;
            index = Mathf.Clamp(index, 0, agentPrefabs.Count - 1);
            return agentPrefabs[index];
        }
    }
}