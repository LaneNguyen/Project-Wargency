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

        [Header("Sprite Hit Test (Optional)")]
        [Tooltip("Bật để chỉ cho kéo khi trỏ chuột đúng vào pixel có alpha > threshold của sprite (pixel-perfect).")]
        [SerializeField] private bool usePixelPerfectHit = true;
        [Range(0f, 1f)]
        [SerializeField] private float alphaThreshold = 0.1f;

        [Tooltip("Nếu texture không bật Read/Write (không readable) thì vẫn cho phép kéo để không chặn nhầm.")]
        [SerializeField] private bool allowDragIfTextureNotReadable = true;

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
            if (agent == null) agent = GetComponent<CharacterAgent>(); // cố gắng tự bám agent gần nhất cho tiện
        }

        // bắt đầu kéo từ world => bật ghost trên canvas và highlight nhân vật
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (agent == null) { Debug.LogWarning("[WorldCharacterDraggable] Missing agent"); return; }

            //nếu bật pixel-perfect thì kiểm tra vị trí có nằm đúng pixel hiển thị không
            if (usePixelPerfectHit && !IsPointerOnVisiblePixel(eventData))
            {
                if (logDebug) Debug.Log("[WorldCharacterDraggable] Skip BeginDrag: pointer không trúng pixel hiển thị");
                return;
            }

            if (logDebug) Debug.Log("[WorldCharacterDraggable] BeginDrag world");

            UIDragContext.BeginDrag(agent);
            if (CursorManager.Instance != null) CursorManager.Instance.SetDraggingCursor();

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

        public void OnDrag(PointerEventData eventData)
        {
            if (ghost != null) ghost.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData) { CancelDrag(); }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (isHighlighted || ghost != null || UIDragContext.CurrentAgent != null) CancelDrag();
        }

        private void OnDisable() { CancelDrag(); }

        private void CancelDrag()
        {
            UIDragContext.EndDrag();
            if (CursorManager.Instance != null) CursorManager.Instance.FlashReleasedCursor();

            if (ghost != null)
            {
                Destroy(ghost.gameObject);
                ghost = null; ghostImg = null;
            }
            StopAllCoroutines();
            RestoreHighlight();
        }

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

        // ===== Pixel-perfect sprite hit test =====
        private bool IsPointerOnVisiblePixel(PointerEventData eventData)
        {
            if (renderers == null || renderers.Length == 0) return true; // không có renderer thì cho qua
            Camera cam = Camera.main;
            if (cam == null) return true; // không có camera → không block

            Vector3 screenPos = eventData.position;
            float z = Mathf.Abs(cam.transform.position.z - transform.position.z);
            Vector3 worldPoint = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, z));

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r.sprite == null) continue;

                Vector3 local = r.transform.InverseTransformPoint(worldPoint);

                Sprite s = r.sprite;
                float ppu = s.pixelsPerUnit;
                Vector2 pivotPx = s.pivot;
                float px = local.x * ppu + pivotPx.x;
                float py = local.y * ppu + pivotPx.y;

                Rect rect = s.rect;
                if (px < 0f || py < 0f || px >= rect.width || py >= rect.height)
                    continue; // ngoài khung sprite

                // map sang toạ độ texture thực (atlas)
                int texX = Mathf.FloorToInt(s.textureRect.x + px);
                int texY = Mathf.FloorToInt(s.textureRect.y + py);

                Texture2D tex = s.texture;
                if (tex == null) continue;

                // nếu texture không readable → cho phép kéo (tránh chặn nhầm)
                if (!tex.isReadable)
                {
                    if (logDebug) Debug.LogWarning("[WorldCharacterDraggable] Texture không readable → cho phép kéo (fallback).");
                    return allowDragIfTextureNotReadable;
                }

                try
                {
                    Color c = tex.GetPixel(texX, texY); // yêu cầu Read/Write Enabled
                    if (c.a > alphaThreshold) return true; // trúng pixel hiển thị
                }
                catch
                {
                    //nếu GetPixel lỗi → cũng cho phép kéo
                    if (logDebug) Debug.LogWarning("[WorldCharacterDraggable] GetPixel() fail → cho phép kéo (fallback).");
                    return allowDragIfTextureNotReadable;
                }
            }
            return false; // không trúng pixel hiển thị nào
        }
    }
}
