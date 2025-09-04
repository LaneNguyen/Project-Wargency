using UnityEngine;
using TMPro;

// widget chính thức để show balance ngân sách
// lắng nghe BudgetController bắn sự kiện thay đổi
// đổi text có dấu phẩy kiểu 1,234 cho dễ nhìn
// UI => Manager => Gameplay truyền giá trị qua event

namespace Wargency.UI
{
    // Widget hiển thị số dư ngân sách.
    //- Đăng ký lắng nghe BudgetController.OnBudgetChanged để cập nhật.
    [DisallowMultipleComponent]
    public class UIBudgetWidget : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI balanceText;

        private void OnEnable()
        {
            if (Wargency.Gameplay.BudgetController.I != null)
            {
                Wargency.Gameplay.BudgetController.I.OnBudgetChanged += Refresh;
                Refresh(Wargency.Gameplay.BudgetController.I.Balance);
            }
        }

        private void OnDisable()
        {
            if (Wargency.Gameplay.BudgetController.I != null)
                Wargency.Gameplay.BudgetController.I.OnBudgetChanged -= Refresh;
        }

        private void Refresh(int newBalance)
        {
            if (balanceText != null)
                balanceText.text = newBalance.ToString("N0");
        }
    }
}