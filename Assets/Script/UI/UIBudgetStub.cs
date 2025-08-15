using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wargency.Gameplay;

namespace Wargency.UI
{
    /// <summary>
    /// Tạm thời hiển thị Budget (Ngân sách) lên UI.
    /// Sau này sẽ thay bằng UI thật.
    /// </summary>
    public class UIBudgetStub : MonoBehaviour
    {
        // Tham chiếu đến Text component trên UI (gán trong Inspector)
        [SerializeField] private TextMeshProUGUI budgetText;

        private void Start()
        {
            //nếu chưa gán text/ tạo tạm thời
            if (budgetText == null)
            {
                GameObject go = new GameObject("BudgetText");
                go.transform.SetParent(this.transform);
                budgetText = go.AddComponent<TextMeshProUGUI>();
                //budgetText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                budgetText.color = Color.black;
            }

            // Lắng nghe event từ GameLoopController
            GameLoopController.Instance.OnBudgetChanged += UpdateBudget;
            UpdateBudget(GameLoopController.Instance.Budget);
        }

        private void OnDestroy()
        {
            if (GameLoopController.Instance != null)
                GameLoopController.Instance.OnBudgetChanged -= UpdateBudget;  // Hủy đăng ký event khi object bị xóa
        }
        /// Hàm được gọi khi Budget thay đổi
        private void UpdateBudget(int newBudget)
        {
            budgetText.text = $"${newBudget}";
        }

    }

}