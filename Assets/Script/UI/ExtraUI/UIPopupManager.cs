using UnityEngine;
using TMPro;

// tạo text popup ở container rồi cho nó bay bằng UIPopupFloat
// hàm ShowText và ShowCenter 
// UI => Manager => Gameplay nghe gọi nhau qua singleton I

namespace Wargency.Gameplay
{
    public class UIPopupManager : MonoBehaviour
    {
        public static UIPopupManager I { get; private set; }

        [Header("Text Popup")]
        [SerializeField] private RectTransform popupTextPrefab;
        [SerializeField] private RectTransform container;

        [Header("Money VFX")]
        [SerializeField] private GameObject coinVFXPrefab;   // prefab rớt tiền (Particle/Animator), đặt dưới Container
        [SerializeField] private float coinVFXLifetime = 1.2f;
        [SerializeField] private Vector2 moneyTextOffset = new Vector2(0f, 24f);
        [SerializeField] private Color moneyTextColor = new Color(1f, 0.9f, 0.2f);

        [Header("Canvas/Camera")]
        [SerializeField] private Canvas targetCanvas;        // canvas của container
        [SerializeField] private Camera uiCamera;            // để convert nếu canvas = ScreenSpace-Camera/World

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

        // ==== MONEY API ====
        // Gọi khi cộng tiền, dùng vị trí UI (anchored) sẵn có
        public void ShowMoneyAtAnchored(int amount, Vector2 anchoredPos)
        {
            SpawnCoinVFX(anchoredPos);
            ShowText("+" + amount.ToString("N0"), moneyTextColor, anchoredPos + moneyTextOffset);
        }
        // Gọi khi cộng tiền từ vị trí screen point (ví dụ vị trí click)
        public void ShowMoneyFromScreenPoint(int amount, Vector2 screenPoint)
        {
            if (!container) return;
            Vector2 anchored;
            if (ScreenToAnchored(screenPoint, out anchored))
                ShowMoneyAtAnchored(amount, anchored);
        }
        // Gọi khi cộng tiền từ vị trí world (ví dụ object trong world)
        public void ShowMoneyFromWorld(int amount, Vector3 worldPos, Camera worldCamera)
        {
            if (!container) return;
            var sp = (worldCamera ? worldCamera : Camera.main).WorldToScreenPoint(worldPos);
            ShowMoneyFromScreenPoint(amount, sp);
        }
        // Overload nhanh: chỉ có amount -> hiện giữa màn hình
        public void ShowMoney(int amount)
        {
            ShowMoneyAtAnchored(amount, Vector2.zero);
        }

        private void SpawnCoinVFX(Vector2 anchoredPos)
        {
            if (!coinVFXPrefab || !container) return;

            // tạo dưới container để bám theo UI
            var go = Instantiate(coinVFXPrefab, container);
            var rt = go.transform as RectTransform;
            if (rt != null) rt.anchoredPosition = anchoredPos;

            if (coinVFXLifetime > 0f) Destroy(go, coinVFXLifetime);
        }

        // Convert screen point sang anchored pos theo canvas hiện tại
        private bool ScreenToAnchored(Vector2 screenPos, out Vector2 anchored)
        {
            anchored = Vector2.zero;
            if (!targetCanvas || !container) return false;

            RectTransform canvasRect = targetCanvas.transform as RectTransform;
            Vector2 local;
            var cam = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : (uiCamera ? uiCamera : Camera.main);

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, cam, out local))
            {
                // local tính theo canvas; đổi sang anchored pos của container
                Vector2 pivotOffset = new Vector2(
                    (0.5f - container.pivot.x) * container.rect.width,
                    (0.5f - container.pivot.y) * container.rect.height
                );

                anchored = local - (Vector2)container.localPosition + pivotOffset;
                return true;
            }
            return false;
        }

    }
}
