using UnityEngine;

namespace Wargency.Gameplay
{
    // Define 1 Wave = 1 Quý; dùng để set mục tiêu & thưởng.

    [CreateAssetMenu(fileName = "WaveDefinition", menuName = "Wargency/WaveDefinition")]
    public class WaveDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id; //Vd: q1,q2
        public string displayName; //Qúy 1, quý 2

        [Header("Progression")]
        public int targetScore = 100; //Điểm cần đạt để hoàn thành wave

        [Header("Reward")]
        public int rewardBudget = 100; // Thưởng budget
    }
}
