using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wargency.UI
{
    [DisallowMultipleComponent]
    public class StoryboardPanel : MonoBehaviour
    {
        [System.Serializable]
        public class Frame
        {
            public Sprite image;
            [TextArea(1, 3)] public string caption;
            [Min(0.1f)] public float duration = 3f;
        }

        [Header("Frames")]
        [SerializeField] private List<Frame> frames = new();

        [Header("UI (assign sẵn hoặc để trống để auto-create)")]
        [SerializeField] private Image currentImage;              // lớp ảnh hiện tại (alpha 1)
        [SerializeField] private Image nextImage;                 // lớp ảnh kế (alpha 0)
        [SerializeField] private TextMeshProUGUI captionUI;       // caption hiển thị

        [Header("Transition")]
        // thêm config
        [SerializeField][Min(0f)] private float firstFadeTime = 0.5f;
        [SerializeField][Min(0f)] private float crossfadeTime = 0.5f; // thời gian blend mỗi lần chuyển
        [SerializeField] private bool autoPlayOnAwake = true;
        [SerializeField] private bool hideOnFinish = true;

        public event System.Action OnFinished;

        private CanvasGroup _rootGroup;
        private Coroutine _routine;

        private void Awake()
        {
            EnsureUI();
            if (autoPlayOnAwake) Play();
        }

        private void OnDisable()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
        }

        // ===== Public API =====
        public void Play()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(PlayRoutine());
        }

        public void ClearFrames() => frames?.Clear();
        public void AddFrame(Sprite sprite, string caption, float duration = 3f)
        {
            frames ??= new List<Frame>();
            frames.Add(new Frame { image = sprite, caption = caption, duration = Mathf.Max(0.1f, duration) });
        }

        private void AttachCoverMode(Image img)
        {
            var arf = img.GetComponent<AspectRatioFitter>();
            if (!arf) arf = img.gameObject.AddComponent<AspectRatioFitter>();
            arf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; // COVER
        }

        private void UpdateAspect(Image img)
        {
            var arf = img.GetComponent<AspectRatioFitter>();
            if (!arf) return;
            var sp = img.sprite;
            if (sp && sp.rect.height > 0)
                arf.aspectRatio = sp.rect.width / sp.rect.height;
        }

        // ===== Core =====
        private IEnumerator PlayRoutine()
        {
            if (frames == null || frames.Count == 0)
                yield break;

            // khung đầu
            if (currentImage)
            {
                currentImage.sprite = frames[0].image;
                UpdateAspect(currentImage);
                currentImage.preserveAspect = true;
                // alpha 0 để fade-in
                var c = currentImage.color; c.a = 0f; currentImage.color = c;
                UpdateAspect(currentImage); // (mục 2 bên dưới)
            }
            if (nextImage)
            {
                var c = nextImage.color; c.a = 0f; nextImage.color = c;
                nextImage.sprite = null;
            }
            if (captionUI)
            {
                captionUI.text = frames[0].caption ?? "";
                captionUI.alpha = 0f; // caption cũng fade-in
            }

            // FADE-IN khung đầu
            float t0 = 0f;
            while (t0 < 1f)
            {
                t0 += (firstFadeTime <= 0f ? 1f : Time.unscaledDeltaTime / firstFadeTime);
                float a = Mathf.Clamp01(t0);
                if (currentImage) currentImage.color = new Color(1, 1, 1, a);
                if (captionUI) captionUI.alpha = a;
                yield return null;
            }

            // giữ khung đầu theo duration
            yield return Hold(frames[0].duration);

            // các khung tiếp theo crossfade
            for (int i = 1; i < frames.Count; i++)
            {
                var f = frames[i];
                yield return CrossfadeTo(f);
                yield return Hold(f.duration);
            }

            OnFinished?.Invoke();
            if (hideOnFinish) gameObject.SetActive(false);
            _routine = null;
        }

        private IEnumerator CrossfadeTo(Frame f)
        {
            if (!currentImage) yield break;

            // set next layer
            if (nextImage == null)
            {
                // nếu thiếu nextImage, tạo nhanh
                nextImage = DuplicateImageSibling(currentImage, "NextImage");
                var c0 = nextImage.color; c0.a = 0f; nextImage.color = c0;
            }

            nextImage.sprite = f.image;
            UpdateAspect(nextImage);
            nextImage.preserveAspect = true;

            // caption crossfade
            string newText = f.caption ?? "";
            float t = 0f;
            Color ci0 = currentImage.color;
            Color ni0 = nextImage.color;
            float cStartA = captionUI ? captionUI.alpha : 1f;

            while (t < 1f)
            {
                t += (crossfadeTime <= 0f ? 1f : Time.unscaledDeltaTime / crossfadeTime);
                float a = Mathf.Clamp01(t);

                if (currentImage) currentImage.color = new Color(ci0.r, ci0.g, ci0.b, 1f - a);
                if (nextImage) nextImage.color = new Color(ni0.r, ni0.g, ni0.b, a);

                if (captionUI)
                {
                    // nửa đầu: fade out, nửa sau: đổi text + fade in
                    float ca = 1f - Mathf.SmoothStep(0f, 1f, a * 2f);
                    if (a >= 0.5f && captionUI.text != newText)
                    {
                        captionUI.text = newText;
                    }
                    float cb = Mathf.SmoothStep(0f, 1f, (a - 0.5f) * 2f);
                    captionUI.alpha = Mathf.Lerp(cStartA, 0f, a < 0.5f ? a * 2f : 1f) * (a < 0.5f ? 1f : 0f)
                                      + (a >= 0.5f ? cb : 0f);
                }

                yield return null;
            }

            // swap: next trở thành current
            var temp = currentImage;
            currentImage = nextImage;
            nextImage = temp;

            // reset layer dưới (giờ là next) alpha 0
            if (nextImage) { var c = nextImage.color; c.a = 0f; nextImage.color = c; nextImage.sprite = null; }

            // đảm bảo caption fully visible
            if (captionUI) captionUI.alpha = 1f;
        }

        private IEnumerator Hold(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime; // chạy kể cả khi game pause
                yield return null;
            }
        }

        // ===== Helpers =====
        private void EnsureUI()
        {
            _rootGroup = GetComponent<CanvasGroup>();
            if (_rootGroup == null) _rootGroup = gameObject.AddComponent<CanvasGroup>();

            if (currentImage == null)
                currentImage = CreateImageChild("CurrentImage", new Vector2(0.05f, 0.25f), new Vector2(0.95f, 0.95f), 1f);

            if (nextImage == null)
                nextImage = CreateImageChild("NextImage", new Vector2(0.05f, 0.25f), new Vector2(0.95f, 0.95f), 0f);
            // sau khi tạo currentImage / nextImage
            AttachCoverMode(currentImage);
            AttachCoverMode(nextImage);
            if (captionUI == null)
            {
                var go = new GameObject("Caption", typeof(RectTransform), typeof(TextMeshProUGUI));
                var rt = (RectTransform)go.transform;
                rt.SetParent(transform, false);
                rt.anchorMin = new Vector2(0.05f, 0.05f);
                rt.anchorMax = new Vector2(0.95f, 0.22f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                captionUI = go.GetComponent<TextMeshProUGUI>();
                captionUI.alignment = TextAlignmentOptions.Center;
                captionUI.enableAutoSizing = true;
                captionUI.fontSizeMin = 14;
                captionUI.fontSizeMax = 34;
                captionUI.raycastTarget = false;
                captionUI.text = "";
            }
        }

        private Image CreateImageChild(string name, Vector2 anchorMin, Vector2 anchorMax, float alpha)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            var c = img.color; c.a = Mathf.Clamp01(alpha); img.color = c;
            img.preserveAspect = true;
            return img;
        }

        private Image DuplicateImageSibling(Image source, string newName)
        {
            var go = new GameObject(newName, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(source.transform.parent, false);
            rt.anchorMin = source.rectTransform.anchorMin;
            rt.anchorMax = source.rectTransform.anchorMax;
            rt.offsetMin = source.rectTransform.offsetMin;
            rt.offsetMax = source.rectTransform.offsetMax;
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.color = new Color(1f, 1f, 1f, 0f);
            return img;
        }
    }
}
