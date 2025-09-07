using System;
using UnityEngine;
using Wargency.Systems;

namespace Wargency.Gameplay
{
    // ví tiền của team, có thêm bớt là báo UI liền
    // TrySpend sẽ tự kiểm tra đủ tiền chưa, cho an toàn
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public class BudgetController : MonoBehaviour, IResettable
    {
        public static BudgetController I { get; private set; }

        [Header("Start Value")]
        [Tooltip("tiền ban đầu khi vào game")]
        [SerializeField] private int startBalance = 0;

        // số dư hiện tại
        public int Balance { get; private set; }

        // ai quan tâm số dư thì nghe cái này
        public event Action<int> OnBudgetChanged;

        private void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            Balance = Mathf.Max(0, startBalance); // âm nhìn sợ quá nên chặn
        }

        private void Start()
        {
            // báo số tiền ban đầu để UI hiển thị liền
            OnBudgetChanged?.Invoke(Balance);
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0) return true; // trừ số lạ thì coi như không làm gì
            if (Balance < amount) return false; // thiếu tiền thì chịu
            Balance -= amount;
            OnBudgetChanged?.Invoke(Balance);
            return true;
        }

        public void Add(int amount)
        {
            if (amount <= 0) return; // cộng số lạ thì thôi
            Balance += amount;
            OnBudgetChanged?.Invoke(Balance);
        }

        public bool CanAfford(int amount) => Balance >= amount; // hỏi nhanh: đủ tiền hông

        public void ResetState()
        {
            Balance = startBalance;
        }
    }
}
