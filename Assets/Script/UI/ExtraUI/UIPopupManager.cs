using UnityEngine;
using TMPro;

// tạo text popup ở container rồi cho nó bay bằng UIPopupFloat
// có hàm ShowText và ShowCenter dùng rất lẹ
// UI => Manager => Gameplay nghe gọi nhau qua singleton I

namespace Wargency.Gameplay
{
    public class UIPopupManager : MonoBehaviour
    {
        public static UIPopupManager I { get; private set; }

        [SerializeField] private RectTransform popupTextPrefab;
        [SerializeField] private RectTransform container;

        private void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
        }

        public void ShowText(string msg, Color color, Vector2 anchoredPos)
        {
            if (!popupTextPrefab || !container) return;
            var inst = Instantiate(popupTextPrefab, container);
            var text = inst.GetComponentInChildren<TMP_Text>(true);
            if (text) { text.text = msg; text.color = color; }

            var floater = inst.GetComponent<UIPopupFloat>();
            if (floater) floater.PlayFrom(anchoredPos);
            else inst.anchoredPosition = anchoredPos;
        }

        public void ShowCenter(string msg, Color color)
        {
            if (!container) return;
            ShowText(msg, color, Vector2.zero);
        }
    }
}
