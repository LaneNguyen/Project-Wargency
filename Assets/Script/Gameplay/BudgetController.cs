using System;
using UnityEngine;

namespace Wargency.Gameplay
{
    /// <summary>
    /// Hệ thống giữ tiền của agency.
    /// - Dùng TrySpend để trừ an toàn (tự kiểm tra đủ tiền).
    /// - Bắn sự kiện khi số dư thay đổi để UI cập nhật.
    /// </summary>
    [DisallowMultipleComponent]
    public class BudgetController : MonoBehaviour
    {
        public static BudgetController I { get; private set; }

        [Header("Thiết lập khởi tạo")]
        [Tooltip("Số dư ban đầu khi vào game.")]
        [SerializeField] private int startBalance = 0;

        /// <summary>Số dư hiện tại.</summary>
        public int Balance { get; private set; }

        /// <summary>Sự kiện: gọi khi Balance đổi.</summary>
        public event Action<int> OnBudgetChanged;

        private void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            Balance = Mathf.Max(0, startBalance);
        }

        private void Start()
        {
            // bắn event 1 lần để UI nhận giá trị ban đầu
            OnBudgetChanged?.Invoke(Balance);
        }

        /// <summary>
        /// Kiểm tra & trừ tiền an toàn. Đủ tiền -> trừ và bắn event, ngược lại trả false.
        /// </summary>
        public bool TrySpend(int amount)
        {
            if (amount <= 0) return true;               // không trừ các giá trị không hợp lệ/âm/0
            if (Balance < amount) return false;         // không đủ tiền
            Balance -= amount;
            OnBudgetChanged?.Invoke(Balance);
            return true;
        }

        /// <summary>Cộng tiền và bắn event (bỏ qua nếu amount <= 0).</summary>
        public void Add(int amount)
        {
            if (amount <= 0) return;
            Balance += amount;
            OnBudgetChanged?.Invoke(Balance);
        }

        /// <summary>Kiểm tra nhanh có đủ tiền hay không.</summary>
        public bool CanAfford(int amount) => Balance >= amount;
    }
}