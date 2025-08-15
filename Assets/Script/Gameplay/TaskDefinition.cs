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

        [Header("Runtime")]
        [Min(0.1f)] public float durationSecond = 10f; //thời gian làm xong task
        public int budgetReward = 0; //phần thưởng tiền
        public int scoreReward = 0; //điểm
        public int energyCost = 0; //năng lượng
        public int stressImpact = 0;// stress và mental
    }
}
