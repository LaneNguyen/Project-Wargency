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
        [SerializeField]
        private float scaleNumber = 1f;
        [SerializeField]
        private float scaleTime = 1.5f;

        private Vector3 originalScale;       // scale gốc của text
        private Coroutine pulseCo;           // coroutine đang chạy

        private void Awake()
        {
            if (balanceText != null)
                originalScale = balanceText.rectTransform.localScale;
        }

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

            // chạy hiệu ứng scale
            if (balanceText != null)
            {
                if (pulseCo != null) StopCoroutine(pulseCo);
                pulseCo = StartCoroutine(PulseScale(balanceText.rectTransform));
            }
        }

        // 0909 Update: Coroutine giật scale lên rồi về lại
        private System.Collections.IEnumerator PulseScale(RectTransform target)
        {
            float dur = scaleTime; // tổng thời gian
            float peak = scaleNumber; // scale tối đa
            float t = 0f;

            // scale lên
            while (t < dur * 0.5f)
            {
                t += Time.unscaledDeltaTime;
                float p = t / (dur * 0.5f);
                float s = Mathf.Lerp(1f, peak, p);
                target.localScale = originalScale * s;
                yield return null;
            }

            t = 0f;
            // scale xuống
            while (t < dur * 0.5f)
            {
                t += Time.unscaledDeltaTime;
                float p = t / (dur * 0.5f);
                float s = Mathf.Lerp(peak, 1f, p);
                target.localScale = originalScale * s;
                yield return null;
            }

            target.localScale = originalScale;
            pulseCo = null;
        }
    }
}