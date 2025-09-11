using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wargency.Gameplay;

namespace Wargency.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public class WorldCharacterSpriteDrag : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private CharacterAgent agent;
        [SerializeField] private bool logDebug = false;

        [Header("Pixel Hit")]
        [SerializeField] private bool usePixelPerfectHit = true;
        [Range(0f, 1f)][SerializeField] private float alphaThreshold = 0.05f;
        [SerializeField] private bool allowDragIfTextureNotReadable = true;

        [Header("Ghost")]
        [SerializeField] private Color ghostTintGray = new Color(0.65f, 0.65f, 0.65f, 0.9f);
        [SerializeField] private Vector2 ghostFallbackSize = new Vector2(96, 96);

        [Header("Highlight")]
        [SerializeField] private bool highlightColorOnly = true;
        [SerializeField] private Color highlightColor = new Color(1f, 1f, 0.85f, 1f);
        [SerializeField] private float highlightScale = 1.05f;

        public static bool GlobalEnable = true; // gate vụ drag ở title screen toàn cục

        // runtime
        private RectTransform ghost;
        private Image ghostImg;
        private SpriteRenderer[] renderers;
        private readonly List<Color> origColors = new List<Color>();
        private Vector3 dragStartScale;
        private bool isHighlighted;
        private bool isDragging;

        private void Awake()
        {
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (agent == null) agent = GetComponent<CharacterAgent>();
        }

        private void OnDisable()
        {
            if (isDragging) CancelDrag();
        }

        private void Update()
        {
            if (!GlobalEnable) return; //gate vụ đang ở title

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                // nếu chuột đang trên UI (nút, panel...),
                // return; // mở comment nếu muốn ưu tiên UI
            }

            if (!isDragging && Input.GetMouseButtonDown(0))
            {
                if (agent == null) { if (logDebug) Debug.LogWarning("[SpriteDrag] Missing agent"); return; }

                // Chỉ bắt kéo nếu trúng pixel hiển thị VÀ có camera hợp lệ
                if (!usePixelPerfectHit || IsPointerOnVisiblePixel(Input.mousePosition))
                {
                    BeginDrag(Input.mousePosition);
                }
            }

            if (isDragging && Input.GetMouseButton(0))
            {
                if (ghost != null) ghost.position = Input.mousePosition;
            }

            if (isDragging && Input.GetMouseButtonUp(0))
            {
                EndDragAndTryDrop(Input.mousePosition);
            }
        }

        private void BeginDrag(Vector2 screenPos)
        {
            isDragging = true;
            UIDragContext.BeginDrag(agent);
            if (CursorManager.Instance != null) CursorManager.Instance.SetDraggingCursor();

            var canvas = ResolveGhostCanvas();
            if (canvas == null)
            {
                if (logDebug) Debug.LogWarning("[SpriteDrag] No canvas found to place ghost");
                // vẫn cho kéo logic nhưng không có ghost
            }
            else
            {
                ghost = new GameObject("DragGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image))
                    .GetComponent<RectTransform>();
                ghost.SetParent(canvas.transform, false);
                ghost.SetAsLastSibling();

                var cg = ghost.GetComponent<CanvasGroup>();
                cg.blocksRaycasts = false;

                ghostImg = ghost.GetComponent<Image>();
                ghostImg.raycastTarget = false;

                Sprite src = (agent != null && agent.IconSprite != null)
                                ? agent.IconSprite
                                : (renderers.Length > 0 ? renderers[0].sprite : null);
                ghostImg.sprite = src;
                ghostImg.color = ghostTintGray;

                ghost.sizeDelta = (src != null) ? new Vector2(src.rect.width, src.rect.height) : ghostFallbackSize;
                ghost.position = screenPos;
                ghost.gameObject.SetActive(true);
            }

            // highlight
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

            if (logDebug) Debug.Log("[SpriteDrag] BeginDrag");
        }

        private void EndDragAndTryDrop(Vector2 screenPos)
        {
            // Raycast UI để tìm IDropHandler
            GameObject dropTarget = null;
            PointerEventData ped = null;

            if (EventSystem.current != null)
            {
                ped = new PointerEventData(EventSystem.current) { position = screenPos };
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(ped, hits);
                if (hits.Count > 0)
                {
                    // chọn node có IDropHandler gần nhất trong hierarchy
                    foreach (var h in hits)
                    {
                        if (ExecuteEvents.GetEventHandler<IDropHandler>(h.gameObject) != null)
                        {
                            dropTarget = ExecuteEvents.GetEventHandler<IDropHandler>(h.gameObject);
                            break;
                        }
                    }
                }
            }

            if (dropTarget != null && ped != null)
            {
                // gọi OnDrop của UI (nhiều drop zone đọc UIDragContext.CurrentAgent)
                ExecuteEvents.ExecuteHierarchy(dropTarget, ped, ExecuteEvents.dropHandler);
                if (logDebug) Debug.Log("[SpriteDrag] Dropped on: " + dropTarget.name);
            }
            else
            {
                if (logDebug) Debug.Log("[SpriteDrag] No drop target under pointer");
            }

            CancelDrag();
        }

        private void CancelDrag()
        {
            UIDragContext.EndDrag();
            if (CursorManager.Instance != null) CursorManager.Instance.FlashReleasedCursor();

            if (ghost != null)
            {
                Destroy(ghost.gameObject);
                ghost = null; ghostImg = null;
            }
            RestoreHighlight();
            isDragging = false;
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

        // ==== Pixel perfect test trên SpriteRenderer (không cần Collider) ====
        private bool IsPointerOnVisiblePixel(Vector2 screenPos)
        {
            if (renderers == null || renderers.Length == 0) return false; //không có renderer thì coi như KHÔNG trúng
            var cam = Camera.main;
            if (cam == null)
            {                          // không có camera => KHÔNG bắt kéo
                if (logDebug) Debug.Log("[SpriteDrag] No Camera.main at Title → block drag");
                return false;
            }
            // dùng plane song song camera để tính worldPoint đúng độ sâu
            // lấy renderer “gốc” làm chuẩn (z gần object)
            float z = Mathf.Abs(cam.transform.position.z - transform.position.z);
            Vector3 worldPoint = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, z));

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r.sprite == null) continue;
                if (!r.isVisible) continue; //chỉ xét renderer đang thấy được

                Vector3 local = r.transform.InverseTransformPoint(worldPoint);

                Sprite s = r.sprite;
                float ppu = s.pixelsPerUnit;
                Vector2 pivotPx = s.pivot;
                float px = local.x * ppu + pivotPx.x;
                float py = local.y * ppu + pivotPx.y;

                Rect rect = s.rect;
                if (px < 0f || py < 0f || px >= rect.width || py >= rect.height)
                    continue;

                int texX = Mathf.FloorToInt(s.textureRect.x + px);
                int texY = Mathf.FloorToInt(s.textureRect.y + py);

                var tex = s.texture;
                if (tex == null) continue;

                // nếu không readable → KHÔNG coi là trúng (tránh auto-true ở Title)
                if (!tex.isReadable) return false;


                try
                {
                    Color c = tex.GetPixel(texX, texY);
                    if (c.a > alphaThreshold) return true;
                }
                catch
                {
                    if (logDebug) Debug.LogWarning("[SpriteDrag] GetPixel fail → allow=" + allowDragIfTextureNotReadable);
                    return allowDragIfTextureNotReadable;
                }
            }
            return false;
        }
    }
}
