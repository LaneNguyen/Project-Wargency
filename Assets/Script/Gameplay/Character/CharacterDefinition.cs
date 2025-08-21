using UnityEngine;


namespace Wargency.Gameplay
{
    [CreateAssetMenu(fileName = "CharacterDefinition", menuName = "Wargency/Character")]

    public class CharacterDefinition : ScriptableObject
    {
        [Header("Định danh")]
        [SerializeField, Tooltip("Unique id, dùng nội bộ")] private string id = "char_001";
        [SerializeField, Tooltip("Tên hiển thị")] private string displayName = "Designer";

        [Header("Visual nhân vật")]
        [SerializeField, Tooltip("Ảnh chân dung cho UI/Avatar")] private Sprite avatar;// đang cân nhắc vụ này
        [SerializeField, Tooltip("Sprite body in-scene")] private Sprite body;

        [Header("Chỉ số")]
        [Min(0)][SerializeField] private int baseEnergy = 100;
        [Min(0)][SerializeField] private int baseStress = 0;
        [Min(0f)][SerializeField] private float moveSpeed = 3.5f;


        [Header("Role class nhân vật")]
        [SerializeField, Tooltip("Copy/Designer/Account/Planer đồ")] private string roleTag = "Account";

        public string Id => id;
        public string DisplayName => displayName;
        public Sprite Avatar => avatar;
        public Sprite Body => body;
        public int BaseEnergy => baseEnergy;
        public int BaseStress => baseStress;
        public float MoveSpeed => moveSpeed;
        public string RoleTag => roleTag;

    }
}