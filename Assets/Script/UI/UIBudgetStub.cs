using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wargency.Gameplay;

// chỉ là stub tạm để show budget thôi
// sau này sẽ thay bằng widget xịn hơn nên đừng lo
// vẫn nghe ngóng budget từ GameLoop để update text
// UI => Manager => Gameplay gắn kết qua event budget

namespace Wargency.UI
{
    // Tạm thời hiển thị Budget (Ngân sách) lên UI.
    // Sau này sẽ thay bằng UI thật.
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
        // Hàm được gọi khi Budget thay đổi
        private void UpdateBudget(int newBudget)
        {
            budgetText.text = $"${newBudget}";
        }

    }

}