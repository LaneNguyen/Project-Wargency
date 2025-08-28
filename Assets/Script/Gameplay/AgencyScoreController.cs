using System;
using UnityEngine;

namespace Wargency.Gameplay
{
    /// <summary>
    /// Điểm uy tín của agency. Dùng để mở khoá/mốc.
    /// - Gọi AddScore để cộng điểm. Bắn sự kiện khi điểm đổi.
    /// </summary>
    [DisallowMultipleComponent]
    public class AgencyScoreController : MonoBehaviour
    {
        public static AgencyScoreController I { get; private set; }

        [Header("Thiết lập khởi tạo")]
        [Tooltip("Điểm khởi tạo khi vào game.")]
        [SerializeField] private int startScore = 0;

        /// <summary>Điểm hiện tại.</summary>
        public int Score { get; private set; }

        /// <summary>Sự kiện: gọi khi Score đổi.</summary>
        public event Action<int> OnScoreChanged;

        private void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            Score = Mathf.Max(0, startScore);
        }

        private void Start()
        {
            // bắn event 1 lần để UI nhận giá trị ban đầu
            OnScoreChanged?.Invoke(Score);
        }

        /// <summary>Cộng điểm và bắn event (bỏ qua nếu amount <= 0).</summary>
        public void AddScore(int amount)
        {
            if (amount <= 0) return;
            Score += amount;
            OnScoreChanged?.Invoke(Score);
        }
    }
}