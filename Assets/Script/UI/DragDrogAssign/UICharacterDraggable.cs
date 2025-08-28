using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wargency.Gameplay;

namespace Wargency.UI
{

    // Kéo-thả avatar để gán vào TaskPanel.
    // Thêm cơ chế "trễ" & "retry" để lấy Agent vì HUDWireUp thường gán Agent SAU OnEnable().

    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(Image))]
    public class UICharacterDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Auto-Bind Agent")]
        [SerializeField] private AutoBindMode autoBind = AutoBindMode.TryAll;
        [SerializeField] private bool autoRetryInStart = true;
        [SerializeField] private bool retryOnBeginDrag = true;
        [Header("Tham chiếu")]
        [SerializeField] private CharacterAgent agent;                 // Sẽ được tự gán nếu để trống
        [SerializeField] private Image avatarImage;                    // Ảnh avatar để hiển thị khi kéo
        [SerializeField] private RectTransform dragGhostPrefab;        // Bóng mờ theo chuột (optional)
        [SerializeField] private bool bringToFrontOnDrag = true;       // Đưa object lên trên cùng khi kéo

        private Canvas rootCanvas;
        private CanvasGroup canvasGroup;
        private RectTransform dragGhost;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            rootCanvas = GetComponentInParent<Canvas>();

            var img = GetComponent<Image>();
            if (img != null) img.raycastTarget = true;
        }

        private void OnEnable()
        {
            if (agent == null && autoBind != AutoBindMode.None)
                TryAutoBindAgent(false); // không warn ở OnEnable

            if (avatarImage != null && avatarImage.sprite == null && agent != null && agent.IconSprite != null)
                avatarImage.sprite = agent.IconSprite;
        }

        private void Start()
        {
            if (autoRetryInStart && agent == null && autoBind != AutoBindMode.None)
                TryAutoBindAgent(true); // warn nhẹ
        }

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
                Debug.LogWarning("[UICharacterDraggable] Chưa tìm thấy Agent. Kiểm tra UIHudWireUp đã gán HUD.Agent chưa.");
        }

        public void Bind(CharacterAgent a)
        {
            agent = a;
            if (avatarImage != null && avatarImage.sprite == null && agent != null && agent.IconSprite != null)
                avatarImage.sprite = agent.IconSprite;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            if (retryOnBeginDrag && agent == null && autoBind != AutoBindMode.None)
                TryAutoBindAgent(true);

            if (agent == null)
            {
                Debug.LogWarning("[UICharacterDraggable] Bắt đầu kéo nhưng chưa có Agent (sau khi retry).");
                return;
            }

            UIDragContext.BeginDrag(agent);

            canvasGroup.alpha = 0.6f;
            canvasGroup.blocksRaycasts = false;

            if (bringToFrontOnDrag)
                transform.SetAsLastSibling();

            if (dragGhostPrefab != null && rootCanvas != null)
            {
                dragGhost = Instantiate(dragGhostPrefab, rootCanvas.transform);

                var cg = dragGhost.GetComponent<CanvasGroup>();
                if (cg == null) cg = dragGhost.gameObject.AddComponent<CanvasGroup>();
                cg.blocksRaycasts = false;

                var ghostImg = dragGhost.GetComponentInChildren<Image>();
                if (ghostImg != null && avatarImage != null && avatarImage.sprite != null)
                    ghostImg.sprite = avatarImage.sprite;

                dragGhost.gameObject.SetActive(true);
                dragGhost.position = eventData.position;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragGhost != null)
                dragGhost.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            UIDragContext.EndDrag();

            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;

            if (dragGhost != null)
            {
                Destroy(dragGhost.gameObject);
                dragGhost = null;
            }
        }
    }

    public static class UIDragContext
    {
        public static CharacterAgent CurrentAgent { get; private set; }
        public static void BeginDrag(CharacterAgent agent) => CurrentAgent = agent;
        public static void EndDrag() => CurrentAgent = null;
    }

    public enum AutoBindMode { None, TryAll }
}
