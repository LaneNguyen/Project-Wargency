using UnityEngine;

namespace Wargency.Gameplay
{
    // Script để làm task cho ScriptableObject authoring).
    [CreateAssetMenu(fileName = "TaskDefinition", menuName = "Wargency/Task Definition", order = 10)]
    public class TaskDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = System.Guid.NewGuid().ToString();
        public string displayName = "New Task";
        public string DisplayName => displayName;


        [Header("Runtime")]
        [Tooltip("Thời lượng cơ bản hoặc tham số khác của task (giữ đúng mô hình M2)")]
        [SerializeField] private float baseDuration = 10f;
        public float BaseDuration => baseDuration;

        [Header("Role Requirement")]
        [Tooltip("Bật nếu task này cần đúng role mới được nhận.")]
        [SerializeField] private bool useRequiredRole = false;

        [Tooltip("Role bắt buộc nếu 'Use Required Role' được bật.")]
        [SerializeField] private CharacterRole requiredRole = CharacterRole.Designer;

        public bool UseRequiredRole => useRequiredRole;
        public CharacterRole RequiredRole => requiredRole;

        [Header("Runtime")]
        [Min(0.1f)] public float durationSecond = 10f; //thời gian làm xong task
        public int budgetReward = 0; //phần thưởng tiền
        public int scoreReward = 0; //điểm
        public int energyCost = 0; //năng lượng
        public int stressImpact = 0;// stress và mental

        public bool HasRoleRestriction(out CharacterRole role)
        {
            role = requiredRole;
            return useRequiredRole;
        }
    }
}
