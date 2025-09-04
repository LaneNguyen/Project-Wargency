using System.Collections.Generic;
using UnityEngine;

namespace Wargency.Gameplay
{
    // cái khuôn task (SO) nè, giờ có thêm phần chọn wave nào được spawn
    [CreateAssetMenu(fileName = "TaskDefinition", menuName = "Wargency/Task Definition", order = 10)]
    public class TaskDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = System.Guid.NewGuid().ToString();
        public string displayName = "New Task";
        public string DisplayName => displayName;

        [Header("Runtime")]
        [Tooltip("thời lượng cơ bản của task")]
        [SerializeField] private float baseDuration = 10f;
        public float BaseDuration => baseDuration;

        [Header("Role Requirement")]
        [SerializeField] private bool useRequiredRole = false;
        [SerializeField] private CharacterRole requiredRole = CharacterRole.Designer;
        public bool UseRequiredRole => useRequiredRole;
        public CharacterRole RequiredRole => requiredRole;

        [Header("Rewards & Costs")]
        [Min(0.1f)] public float durationSecond = 10f;
        public int RewardMoney = 0;
        public int RewardScore = 0;
        public int energyCost = 0;
        public int stressImpact = 0;

        [Header("Wave Availability")]
        [Tooltip("bật cái này nếu task này chơi mọi wave cho đỡ nghĩ")]
        public bool availableAllWaves = true;

        [Tooltip("nếu không phải mọi wave => liệt kê mấy wave được phép (1,2,3,...)")]
        public List<int> allowedWaves = new List<int>();

        public bool HasRoleRestriction(out CharacterRole role)
        {
            role = requiredRole;
            return useRequiredRole;
        }

        public bool IsAvailableAtWave(int wave)
        {
            if (availableAllWaves) return true;
            if (allowedWaves == null || allowedWaves.Count == 0) return false;
            for (int i = 0; i < allowedWaves.Count; i++)
            {
                if (allowedWaves[i] == wave) return true;
            }
            return false;
        }
    }
}
