using System;
using UnityEngine;

namespace Wargency.Gameplay
{
    // điểm uy tín của agency nè, xài để mở mốc hoặc khoá gì đó
    // gọi AddScore là cộng, có event báo cho UI biết luôn
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public class AgencyScoreController : MonoBehaviour
    {
        public static AgencyScoreController I { get; private set; }

        [Header("Start Value")]
        [Tooltip("điểm ban đầu khi vào game")]
        [SerializeField] private int startScore = 0;

        // điểm hiện tại đang có
        public int Score { get; private set; }

        // ai cần nghe thì đăng ký, mỗi lần điểm đổi sẽ kêu
        public event Action<int> OnScoreChanged;

        private void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            Score = Mathf.Max(0, startScore); // không cho âm cho lành
        }

        private void Start()
        {
            // báo 1 phát để UI nắm số ban đầu
            OnScoreChanged?.Invoke(Score);
        }

        public void AddScore(int amount)
        {
            if (amount <= 0) return; // cộng số kỳ kỳ thì thôi
            Score += amount;
            OnScoreChanged?.Invoke(Score);
        }
    }
}
