using UnityEngine;

namespace Wargency.Gameplay
{
    // file này định nghĩa data cho Event
    // có 2 kiểu => Instant hoặc Choice
    // unlockAtWave dùng mốc WAVE 1-BASED (1 = Wave 1, 2 = Wave 2, 3 = Wave 3...)
    // ***LƯU Ý MỚI: unlockAtWave được hiểu là "xuất hiện Ở ĐÚNG wave đó" theo mặc định mới
    // (match exact). Hành vi này được điều khiển ở EventManager (matchExactWave = true).
    // Nếu muốn hành vi "từ wave X trở đi" thì tắt matchExactWave và bật allowCarryOverFromEarlierWaves.

    [CreateAssetMenu(fileName = "EventDefinition", menuName = "Wargency/Event Definition")]
    public class EventDefinition : ScriptableObject
    {
        public enum EventKind { Instant, Choice }

        // nhóm info => id và text hiển thị
        [Header("Thông tin Event / Identity")]
        public string id;
        public string title;
        [TextArea] public string description;

        // chọn kiểu event => Instant áp liền, Choice cho người chơi chọn
        [Header("Kiểu Event")]
        public EventKind kind = EventKind.Instant;

        // thiết lập mở khoá theo wave => 1 là Wave 1, 2 là Wave 2...
        [Header("Unlock (1-based)")]
        [Tooltip("1 = Wave 1, 2 = Wave 2... (mặc định event chỉ spawn ở đúng wave này)")]
        public int unlockAtWave = 1;

        // tác động dùng cho Instant => áp thẳng budget/score, còn energy/stress chỉ hiện text
        [Header("Hiệu ứng/ tác động thay đổi (Chỉ dùng cho Instant)")]
        public int budgetChange;
        public int scoreChange;
        [Tooltip("Chỉ hiển thị text lên UI: +- Energy team")]
        public int teamEnergyTextDelta;
        [Tooltip("Chỉ hiển thị text lên UI: +- Stress team")]
        public int teamStressTextDelta;

        // 2 lựa chọn khi là Choice => A và B
        [Header("Choice Options (chỉ dùng khi kind = Choice)")]
        public ChoiceOption optionA;
        public ChoiceOption optionB;

        // 1 option trong Choice => có mô tả và tác động khi chọn
        [System.Serializable]
        public class ChoiceOption
        {
            public string optionTitle = "Phương án A/B";
            [TextArea] public string optionDescription;

            [Header("Tác động khi chọn phương án này")]
            public int budgetChange;
            public int scoreChange;
            [Tooltip("Chỉ hiển thị text lên UI: +- Energy team")]
            public int teamEnergyTextDelta;
            [Tooltip("Chỉ hiển thị text lên UI: +- Stress team")]
            public int teamStressTextDelta;
        }

        // thời gian ngẫu nhiên giữa các lần spawn event
        [Header("Spawn Interval (sec)")]
        [Min(0.1f)] public float minIntervalSec = 8f;
        [Min(0.1f)] public float maxIntervalSec = 15f;
    }

    // lưu ý
    // - unlockAtWave là 1-based
    // - hành vi filter theo wave nằm ở EventManager (matchExactWave / allowCarryOverFromEarlierWaves)
}
