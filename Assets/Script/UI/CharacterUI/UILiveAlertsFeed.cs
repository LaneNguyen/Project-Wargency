using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// feed hiển thị thông báo ngắn ngắn
// push chuỗi vào là tự động trượt lên rồi biến mất
// có anti spam theo key để khỏi trùng nhen
namespace Wargency.UI
{
    // feed cảnh báo chạy chữ
    // clone từ template text rồi trượt lên trên
    // có anti spam key để không bị lặp dòng y chang liên tục
    public class UILiveAlertsFeed : MonoBehaviour
    {
        [Header("Template (mốc)")]
        [SerializeField] private TextMeshProUGUI template;     // Text mẫu: đặt đúng vị trí/anchor/pivot/size/style. Để INACTIVE trong scene.
        [SerializeField] private CanvasGroup group;            // CanvasGroup trên panel để fade in/out

        [Header("Layout & Position")]
        [Tooltip("Lấy chính anchoredPosition/anchor/pivot/size của template làm mốc.")]
        [SerializeField] private bool useTemplateAsAnchor = true;

        [Tooltip("Sao chép sizeDelta của template cho từng item (đúng khung như template).")]
        [SerializeField] private bool inheritRectFromTemplate = true;

        [Tooltip("Sao chép style của template (font, fontSize, alignment, wrapping, overflow...).")]
        [SerializeField] private bool inheritStyleFromTemplate = true;

        [Tooltip("Chiều cao 1 dòng; để 0 sẽ lấy từ template.rectTransform.sizeDelta.y")]
        [SerializeField] private float rowHeight = 0f;

        [Tooltip("Khoảng cách giữa các dòng (pixels)")]
        [SerializeField] private float rowSpacing = 6f;

        [Tooltip("Item mới sẽ spawn THẤP HƠN mục tiêu N dòng, rồi trượt lên.")]
        [Min(0f)][SerializeField] private float spawnBelowLines = 1.0f;

        [Tooltip("Giữ mọi item trong khung parent theo chiều dọc (dựa vào height của RectTransform parent).")]
        [SerializeField] private bool clampToParentHeight = false;

        [Tooltip("Chừa mép dưới khi clamp (pixels).")]
        [SerializeField] private float bottomPadding = 0f;

        [Header("Behavior")]
        [SerializeField] private int maxItems = 4;
        [SerializeField] private float lifeTime = 3f;
        [SerializeField] private float fadeOutDuration = 0.35f;
        [SerializeField] private float enterSlideDuration = 0.14f;
        [SerializeField] private float shiftSlideDuration = 0.12f;
        [SerializeField] private float hideDelay = 0.25f;

        [Header("Anti-spam / Dedup")]
        [Tooltip("Không cho phép cùng một key push liên tiếp trong khoảng thời gian này (giây).")]
        [SerializeField] private float cooldownPerKey = 1.5f;

        [Tooltip("Nếu một key đang hiển thị, bỏ qua những push trùng key cho tới khi item đó biến mất.")]
        [SerializeField] private bool suppressWhileActive = true;

        // ===== runtime =====
        private readonly List<TextMeshProUGUI> active = new();                 // oldest ở index 0 (ngay vị trí template), newest ở cuối (dịch xuống)
        private readonly Dictionary<TextMeshProUGUI, Coroutine> moveCos = new();
        private readonly Dictionary<TextMeshProUGUI, Coroutine> lifeCos = new();
        private readonly Dictionary<string, float> _lastShownAt = new();       // key -> last unscaled time
        private readonly HashSet<string> _activeKeys = new();                  // key đang hiển thị

        private Coroutine hideCo;
        private RectTransform parentRT;

        private float Line => (RowH + rowSpacing);
        private float RowH
            => rowHeight > 0f
                ? rowHeight
                : (template ? template.rectTransform.sizeDelta.y : 24f);

        private void Awake()
        {
            if (template) template.gameObject.SetActive(false);
            if (group) SetVisible(false, instant: true);
            parentRT = template ? template.rectTransform.parent as RectTransform : transform as RectTransform;
        }

        // ================= PUBLIC API =================
        public void Push(string msg) => Push(msg, msg); // mặc định lấy chính msg làm key

        public void Push(string msg, string key)
        {
            if (template == null || parentRT == null) return;
            if (string.IsNullOrEmpty(key)) key = msg;

            float now = Time.unscaledTime;

            // Anti-spam
            if (_lastShownAt.TryGetValue(key, out float last) && (now - last) < cooldownPerKey)
                return;
            if (suppressWhileActive && _activeKeys.Contains(key))
                return;
            _lastShownAt[key] = now;

            // Hiện panel
            if (group) SetVisible(true);

            // Full → remove oldest trước
            if (active.Count >= maxItems)
            {
                var oldest = active[0];
                StopItemCoroutines(oldest);
                StartCoroutine(FadeAndRemove(oldest, immediate: false));
            }

            // Tạo item mới: clone từ template
            var inst = Instantiate(template, parentRT);
            inst.text = msg;
            inst.gameObject.SetActive(true);
            SetAlpha(inst, 1f);

            // Gắn key holder
            var kh = inst.gameObject.AddComponent<_KeyHolder>(); kh.key = key;
            _activeKeys.Add(key);

            // RECT: anchor/pivot/size/pos theo template
            var rt = inst.rectTransform;
            var trt = template.rectTransform;

            if (useTemplateAsAnchor)
            {
                rt.anchorMin = trt.anchorMin;
                rt.anchorMax = trt.anchorMax;
                rt.pivot = trt.pivot;
            }
            else
            {
                // fallback: neo top-left
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
            }

            if (inheritRectFromTemplate)
            {
                rt.sizeDelta = trt.sizeDelta;
            }
            else
            {
                rt.sizeDelta = new Vector2(trt.sizeDelta.x, RowH);
            }

            // STYLE: copy style từ template nếu muốn (font, size, align, wrapping... trừ alpha)
            if (inheritStyleFromTemplate)
                CopyTextStyle(template, inst);

            // Tính base theo anchoredPosition của template
            Vector2 basePos = trt.anchoredPosition;

            // vị trí đích của item mới (sẽ là phần tử cuối)
            int newIndex = Mathf.Min(active.Count, maxItems - 1);
            float targetY = basePos.y - newIndex * Line; // dịch xuống dần từng dòng
            float targetX = basePos.x;

            // vị trí spawn ban đầu: thấp hơn mục tiêu spawnBelowLines dòng để trượt lên
            float startY = targetY - Line * spawnBelowLines;

            // clamp trong parent nếu cần
            if (clampToParentHeight && parentRT != null)
            {
                float minY = -(parentRT.rect.height - bottomPadding); // đáy
                if (startY < minY) startY = minY;
                if (targetY < minY) targetY = minY;
            }

            rt.anchoredPosition = new Vector2(targetX, startY);

            // thêm vào list
            active.Add(inst);

            // Dịch các item cũ về đúng vị trí mới tính từ template
            for (int i = 0; i < active.Count - 1; i++)
            {
                var t = active[i];
                float y = basePos.y - i * Line;
                float x = basePos.x;
                if (clampToParentHeight && parentRT != null)
                {
                    float minY = -(parentRT.rect.height - bottomPadding);
                    if (y < minY) y = minY;
                }
                SlideTo(t, new Vector2(x, y), shiftSlideDuration);
            }

            // Cho item mới trượt lên vị trí đích
            SlideTo(inst, new Vector2(targetX, targetY), enterSlideDuration);

            // Vòng đời tự hết hạn
            var co = StartCoroutine(LifetimeThenFade(inst));
            lifeCos[inst] = co;
        }

        // ================= LIFETIME =================
        private IEnumerator LifetimeThenFade(TextMeshProUGUI t)
        {
            float elapsed = 0f;
            while (elapsed < lifeTime && t != null)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            if (t != null)
                yield return FadeAndRemove(t, immediate: false);
        }

        private IEnumerator FadeAndRemove(TextMeshProUGUI t, bool immediate)
        {
            if (t == null) yield break;

            // giải phóng key active
            var kh = t.GetComponent<_KeyHolder>();
            if (kh && !string.IsNullOrEmpty(kh.key)) _activeKeys.Remove(kh.key);

            // loại khỏi list
            int idx = active.IndexOf(t);
            if (idx >= 0) active.RemoveAt(idx);

            // dừng coroutines
            StopItemCoroutines(t);

            // fade out
            if (!immediate)
            {
                float t0 = 0f;
                Color c0 = t.color;
                while (t0 < fadeOutDuration && t != null)
                {
                    t0 += Time.unscaledDeltaTime;
                    float a = Mathf.Lerp(1f, 0f, t0 / fadeOutDuration);
                    t.color = new Color(c0.r, c0.g, c0.b, a);
                    yield return null;
                }
            }

            if (t != null) Destroy(t.gameObject);

            // re-layout phần còn lại theo vị trí template
            if (template != null)
            {
                Vector2 basePos = template.rectTransform.anchoredPosition;
                for (int i = 0; i < active.Count; i++)
                {
                    var item = active[i];
                    float y = basePos.y - i * Line;
                    float x = basePos.x;
                    if (clampToParentHeight && parentRT != null)
                    {
                        float minY = -(parentRT.rect.height - bottomPadding);
                        if (y < minY) y = minY;
                    }
                    SlideTo(item, new Vector2(x, y), shiftSlideDuration);
                }
            }

            // hết item → ẩn panel
            if (active.Count == 0 && group)
            {
                if (hideCo != null) StopCoroutine(hideCo);
                hideCo = StartCoroutine(HideAfterDelay());
            }
        }

        // ================= ANIM =================
        private void SlideTo(TextMeshProUGUI t, Vector2 target, float duration)
        {
            if (t == null) return;
            if (moveCos.TryGetValue(t, out var co) && co != null) StopCoroutine(co);
            moveCos[t] = StartCoroutine(SlideCoroutine(t.rectTransform, target, duration));
        }

        private IEnumerator SlideCoroutine(RectTransform rt, Vector2 target, float duration)
        {
            if (rt == null) yield break;
            float t0 = 0f;
            Vector2 start = rt.anchoredPosition;

            if (duration <= 0.0001f)
            {
                rt.anchoredPosition = target;
                yield break;
            }

            while (t0 < duration && rt != null)
            {
                t0 += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t0 / duration);
                u = 1f - Mathf.Pow(1f - u, 3f); // ease-out
                rt.anchoredPosition = Vector2.LerpUnclamped(start, target, u);
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = target;
        }

        // ================= PANEL VIS =================
        private IEnumerator HideAfterDelay()
        {
            float t = 0f;
            while (t < hideDelay)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (active.Count == 0) SetVisible(false);
        }

        private void SetVisible(bool on, bool instant = false)
        {
            if (group == null) return;
            if (instant)
            {
                group.alpha = on ? 1f : 0f;
                group.interactable = on;
                group.blocksRaycasts = on;
                return;
            }
            StartCoroutine(FadeGroup(on ? 1f : 0f));
            group.interactable = on;
            group.blocksRaycasts = on;
        }

        private IEnumerator FadeGroup(float target)
        {
            float dur = 0.2f;
            float t0 = 0f;
            float start = group.alpha;
            while (t0 < dur)
            {
                t0 += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(start, target, t0 / dur);
                yield return null;
            }
            group.alpha = target;
        }

        // ================= UTILS =================
        private void SetAlpha(TextMeshProUGUI t, float a)
        {
            var c = t.color;
            t.color = new Color(c.r, c.g, c.b, a);
        }

        private void StopItemCoroutines(TextMeshProUGUI t)
        {
            if (t == null) return;
            if (moveCos.TryGetValue(t, out var m) && m != null) StopCoroutine(m);
            moveCos.Remove(t);

            if (lifeCos.TryGetValue(t, out var l) && l != null) StopCoroutine(l);
            lifeCos.Remove(t);
        }

        private void CopyTextStyle(TextMeshProUGUI src, TextMeshProUGUI dst)
        {
            if (src == null || dst == null) return;
            dst.font = src.font;
            dst.fontMaterial = src.fontMaterial;
            dst.fontSize = src.fontSize;
            dst.alignment = src.alignment;
            dst.color = src.color;               // alpha sẽ được điều khiển riêng
            dst.overflowMode = src.overflowMode;
            dst.richText = src.richText;
            dst.characterSpacing = src.characterSpacing;
            dst.wordSpacing = src.wordSpacing;
            dst.lineSpacing = src.lineSpacing;
            dst.lineSpacingAdjustment = src.lineSpacingAdjustment;
        }

        // giữ key cho mỗi item để quản lý suppressWhileActive
        private class _KeyHolder : MonoBehaviour { public string key; }
    }
}