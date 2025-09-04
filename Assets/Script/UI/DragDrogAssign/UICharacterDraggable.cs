using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wargency.Gameplay;

namespace Wargency.UI
{
    // file này giúp kéo avatar nhân vật trên UI để gán vô task panel
    // khi kéo thì làm sáng avatar cho dễ thấy => người chơi biết đang kéo bạn này nè
    // tạo một con ma ghost xám đi theo chuột để thả cho sướng tay
    // khi thả thì trả mọi thứ về như cũ để UI không bị kẹt trạng thái
    // giao tiếp UI và Gameplay qua UIDragContext để DropZone biết agent nào đang được kéo

    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(Image))]
    public class UICharacterDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Auto-Bind Agent")]
        [SerializeField] private AutoBindMode autoBind = AutoBindMode.TryAll;
        [SerializeField] private bool autoRetryInStart = true;
        [SerializeField] private bool retryOnBeginDrag = true;

        [Header("Tham chiếu")]
        [SerializeField] private CharacterAgent agent;      // sẽ auto-bind nếu để trống
        [SerializeField] private Image avatarImage;         // ảnh avatar; nếu null sẽ lấy từ chính Image trên object

        [Header("Hiệu ứng kéo")]
        [SerializeField] private bool bringToFrontOnDrag = true;
        [SerializeField] private Color ghostTintGray = new Color(0.65f, 0.65f, 0.65f, 0.9f);
        [SerializeField] private Color highlightColor = new Color(1f, 1f, 0.85f, 1f);
        [SerializeField] private float highlightScale = 1.05f;

        private Canvas rootCanvas;
        private CanvasGroup canvasGroup;
        private RectTransform dragGhost;       // ghost runtime (RectTransform + Image + CanvasGroup)
        private Image ghostImg;

        // lưu để khôi phục khi thả
        private Color origAvatarColor;
        private Vector3 origLocalScale;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            rootCanvas = GetComponentInParent<Canvas>(true);

            if (avatarImage == null) avatarImage = GetComponent<Image>();
            if (avatarImage != null) avatarImage.raycastTarget = true;

            origLocalScale = transform.localScale;
            origAvatarColor = (avatarImage != null) ? avatarImage.color : Color.white;
        }

        private void OnEnable()
        {
            if (agent == null && autoBind != AutoBindMode.None)
                TryAutoBindAgent(false);

            // UI và Gameplay: nếu đã có agent thì mượn IconSprite cho avatar UI
            if (avatarImage != null && avatarImage.sprite == null && agent != null && agent.IconSprite != null)
                avatarImage.sprite = agent.IconSprite;
        }

        private void Start()
        {
            if (autoRetryInStart && agent == null && autoBind != AutoBindMode.None)
                TryAutoBindAgent(true);
        }

        // tìm agent xung quanh để đỡ phải kéo tay dây reference
        private void TryAutoBindAgent(bool logWarningIfFail)
        {
            var hud = GetComponentInParent<UICharacterHUD>();
            if (hud != null && hud.Agent != null)
                agent = hud.Agent;

            if (agent == null)
            {
                var parentAgent = GetComponentInParent<CharacterAgent>();
                if (parentAgent != null) agent = parentAgent;
            }

            if (avatarImage != null && avatarImage.sprite == null && agent != null && agent.IconSprite != null)
                avatarImage.sprite = agent.IconSprite;

            if (logWarningIfFail && agent == null)
                Debug.LogWarning("[UICharacterDraggable] Chưa tìm thấy Agent nha kiểm tra UIHudWireUp đã gán HUD.Agent chưa");
        }

        // cho hệ khác bind thẳng vào đây nếu muốn chủ động
        public void Bind(CharacterAgent a)
        {
            agent = a;
            if (avatarImage != null && avatarImage.sprite == null && agent != null && agent.IconSprite != null)
                avatarImage.sprite = agent.IconSprite;
        }

        // bắt đầu kéo => tạo ghost xám và làm sáng avatar
        // UI và Gameplay: gọi UIDragContext.BeginDrag để DropZone biết ai đang được kéo
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            if (retryOnBeginDrag && agent == null && autoBind != AutoBindMode.None)
                TryAutoBindAgent(true);

            if (agent == null)
            {
                Debug.LogWarning("[UICharacterDraggable] Bắt đầu kéo nhưng chưa có Agent sau khi thử bind lại nha");
                return;
            }

            if (rootCanvas == null)
            {
                rootCanvas = GetComponentInParent<Canvas>(true);
                if (rootCanvas == null)
                {
                    Debug.LogWarning("[UICharacterDraggable] Không tìm thấy Canvas cha để gắn ghost nè");
                    return;
                }
            }

            // báo cho hệ DropZone biết đang kéo agent này
            UIDragContext.BeginDrag(agent);
            if (CursorManager.Instance != null) CursorManager.Instance.SetDraggingCursor(); // UI và Manager: đổi cursor qua trạng thái kéo

            // sáng + scale avatar
            if (avatarImage != null) avatarImage.color = highlightColor;
            transform.localScale = origLocalScale * highlightScale;

            // bật chế độ kéo => đừng chặn raycast phía sau
            canvasGroup.alpha = 0.6f;
            canvasGroup.blocksRaycasts = false;

            if (bringToFrontOnDrag) transform.SetAsLastSibling();

            // tạo ghost runtime từ sprite hiện có
            var sprite = (avatarImage != null && avatarImage.sprite != null)
                         ? avatarImage.sprite
                         : (agent != null ? agent.IconSprite : null);

            dragGhost = new GameObject("DragGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image))
                        .GetComponent<RectTransform>();
            dragGhost.SetParent(rootCanvas.transform, false);
            dragGhost.SetAsLastSibling();

            var ghostCG = dragGhost.GetComponent<CanvasGroup>();
            ghostCG.blocksRaycasts = false;

            ghostImg = dragGhost.GetComponent<Image>();
            ghostImg.raycastTarget = false;
            ghostImg.sprite = sprite;
            ghostImg.color = ghostTintGray;

            // cỡ ghost theo avatar nếu có => nhìn khớp hơn
            if (avatarImage != null)
                dragGhost.sizeDelta = avatarImage.rectTransform.rect.size;
            else if (sprite != null)
                dragGhost.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height);
            else
                dragGhost.sizeDelta = new Vector2(96, 96);

            dragGhost.gameObject.SetActive(true);
            dragGhost.position = eventData.position; // screen space nên set trực tiếp
        }

        // kéo thì ghost chạy theo chuột cho vui
        public void OnDrag(PointerEventData eventData)
        {
            if (dragGhost != null)
                dragGhost.position = eventData.position;
        }

        // thả => dọn context + trả UI về như cũ
        // UI ⇄ Gameplay: UIDragContext.EndDrag để tắt trạng thái kéo toàn cục
        public void OnEndDrag(PointerEventData eventData)
        {
            UIDragContext.EndDrag();
            if (CursorManager.Instance != null) CursorManager.Instance.FlashReleasedCursor(); // UI và Manager: nháy con trỏ thả xong

            // trả avatar về màu cũ và scale cũ
            if (avatarImage != null) avatarImage.color = origAvatarColor;
            transform.localScale = origLocalScale;

            // trả input về bình thường
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;

            // dọn ghost cho sạch
            if (dragGhost != null)
            {
                Destroy(dragGhost.gameObject);
                dragGhost = null;
                ghostImg = null;
            }
        }
    }

    // cái rổ tạm để truyền agent đang kéo giữa các UI
    public static class UIDragContext
    {
        public static CharacterAgent CurrentAgent { get; private set; }
        public static void BeginDrag(CharacterAgent agent) => CurrentAgent = agent; // UI và Gameplay
        public static void EndDrag() => CurrentAgent = null; // UI và Gameplay
    }

    public enum AutoBindMode { None, TryAll }
}
