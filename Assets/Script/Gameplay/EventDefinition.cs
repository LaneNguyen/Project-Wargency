using UnityEngine;

namespace Wargency.Gameplay
{
    // Định nghĩa một Event ngẫu nhiên: tên, mô tả và tác động lên Budget/Score.
    [CreateAssetMenu(fileName = "EventDefinition", menuName = "Wargency/Event Definition")]
    public class EventDefinition : ScriptableObject
    {
        [Header("Thông tin Event/ Identity")]
        public string id;
        public string title;
        [TextArea] public string description;

        [Header("Hiệu ứng/ tác động thay đổi")]
        public int budgetChange;
        public int scoreChange;

        [Header("Spawn Interval (sec)")]
        [Min(0.1f)] public float minIntervalSec = 8f;
        [Min(0.1f)] public float maxIntervalSec = 15f;
    }
}