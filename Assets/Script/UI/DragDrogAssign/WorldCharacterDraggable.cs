using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wargency.Gameplay;

namespace Wargency.UI
{
    // bản này cho phép kéo trực tiếp từ nhân vật ở world lên UI drop zone
    // tạo ghost xám trên canvas để bám theo chuột => nhìn rõ là đang kéo bạn nào
    // highlight tất cả SpriteRenderer con cho sáng lấp lánh cho dễ thấy
    // có nhiều đường thoát để hủy kéo cho an toàn như EndDrag Disable PointerUp
    // giao tiếp UI vs Gameplay qua UIDragContext, giao tiếp UI vs Manager qua CursorManager

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D), typeof(SpriteRenderer))]
    public class WorldCharacterDraggable : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
    {
        [Header("Data")]
        [SerializeField] private CharacterAgent agent;
        [SerializeField] private bool logDebug = false;

        [Header("Ghost")]
        [SerializeField] private Color ghostTintGray = new Color(0.65f, 0.65f, 0.65f, 0.9f);
        [SerializeField] private Vector2 ghostFallbackSize = new Vector2(96, 96);

        [Header("Highlight")]
        [SerializeField] private bool highlightColorOnly = true;
        [SerializeField] private Color highlightColor = new Color(1f, 1f, 0.85f, 1f);
        [SerializeField] private float highlightScale = 1.05f;

        // runtime
        private RectTransform ghost;
        private Image ghostImg;
        private SpriteRenderer[] renderers;
        private readonly List<Color> origColors = new List<Color>();
        private Vector3 dragStartScale;
        private bool isHighlighted;

        private void Awake()
        {
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (agent == null) agent = GetComponent<CharacterAgent>(); // fix: cố gắng tự bám agent gần nhất cho tiện
        }

        // bắt đầu kéo từ world => bật ghost trên canvas và highlight nhân vật
        // UI ⇄ Gameplay: BeginDrag để vùng UI biết đang kéo ai
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (agent == null) { Debug.LogWarning("[WorldCharacterDraggable] Missing agent nè"); return; }

            if (logDebug) Debug.Log("[WorldCharacterDraggable] BeginDrag world");

            UIDragContext.BeginDrag(agent);
            if (CursorManager.Instance != null) CursorManager.Instance.SetDraggingCursor(); // UI ⇄ Manager

            var canvas = ResolveGhostCanvas();
            if (canvas == null) { Debug.LogWarning("[WorldCharacterDraggable] No canvas found để đặt ghost"); return; }

            ghost = new GameObject("DragGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image))
                .GetComponent<RectTransform>();
            ghost.SetParent(canvas.transform, false);
            ghost.SetAsLastSibling();

            var cg = ghost.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = false;

            ghostImg = ghost.GetComponent<Image>();
            ghostImg.raycastTarget = false;

            Sprite src = (agent.IconSprite != null) ? agent.IconSprite :
                         (renderers.Length > 0 ? renderers[0].sprite : null);
            ghostImg.sprite = src;
            ghostImg.color = ghostTintGray;

            ghost.sizeDelta = (src != null) ? new Vector2(src.rect.width, src.rect.height) : ghostFallbackSize;
            ghost.position = eventData.position;
            ghost.gameObject.SetActive(true);

            // highlight toàn bộ renderer con cho sáng
            if (!isHighlighted)
            {
                origColors.Clear();
                dragStartScale = transform.localScale;
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    origColors.Add(r.color);
                    r.color = highlightColor;
                }
                if (!highlightColorOnly)
                    transform.localScale = dragStartScale * Mathf.Max(0.01f, highlightScale);
                isHighlighted = true;
            }
        }

        // kéo thì ghost chạy theo chuột
        public void OnDrag(PointerEventData eventData)
        {
            if (ghost != null) ghost.position = eventData.position;
        }

        // thả chuột thì hủy kéo
        public void OnEndDrag(PointerEventData eventData) { CancelDrag(); }

        // phòng hờ người chơi buông chuột mà không gọi EndDrag
        public void OnPointerUp(PointerEventData eventData)
        {
            if (isHighlighted || ghost != null || UIDragContext.CurrentAgent != null) CancelDrag();
        }

        // disable object cũng hủy kéo cho sạch sẽ
        private void OnDisable() { CancelDrag(); }

        // gom chỗ dọn dẹp kéo vào một nơi cho đỡ sót
        private void CancelDrag()
        {
            UIDragContext.EndDrag(); // UI ⇄ Gameplay
            if (CursorManager.Instance != null) CursorManager.Instance.FlashReleasedCursor(); // UI ⇄ Manager

            if (ghost != null)
            {
                Destroy(ghost.gameObject);
                ghost = null; ghostImg = null;
            }
            StopAllCoroutines(); // fix: nếu có coroutine hiệu ứng thì ngắt để không rò rỉ
            RestoreHighlight();
        }

        // trả màu và scale về như ban đầu
        private void RestoreHighlight()
        {
            if (!isHighlighted) return;
            int count = Mathf.Min(renderers.Length, origColors.Count);
            for (int i = 0; i < count; i++)
            {
                if (renderers[i] != null) renderers[i].color = origColors[i];
            }
            if (!highlightColorOnly)
                transform.localScale = dragStartScale;
            isHighlighted = false;
            origColors.Clear();
        }

        // tìm canvas tốt nhất để đặt ghost cho nó ăn raycast UI chuẩn chỉnh
        private Canvas ResolveGhostCanvas()
        {
            Canvas best = null;
            var all = GameObject.FindObjectsOfType<Canvas>(true);
            foreach (var c in all)
            {
                if (!c.isActiveAndEnabled) continue;
                if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.GetComponent<GraphicRaycaster>() != null)
                { best = c; break; }
            }
            if (best == null)
            {
                foreach (var c in all)
                {
                    if (!c.isActiveAndEnabled) continue;
                    if (c.renderMode == RenderMode.ScreenSpaceCamera && c.worldCamera != null &&
                        c.GetComponent<GraphicRaycaster>() != null)
                    { best = c; break; }
                }
            }
            return best;
        }
    }
}
