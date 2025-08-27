using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Wargency.UI
{
    public class UILiveAlertsFeed : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI template; // 1 dòng ẩn sẵn
        [SerializeField] private int maxItems = 4;
        [SerializeField] private float lifeTime = 3f;
        [SerializeField] private float hideDelay = 0.25f;
        [SerializeField] private CanvasGroup group; // gán CanvasGroup trên panel này

        private readonly Queue<TextMeshProUGUI> items = new();
        private Coroutine hideCo;

        private void Awake()
        {
            if (template) template.gameObject.SetActive(false);
            if (group) SetVisible(false, instant: true);
        }

        public void Push(string msg)
        {
            if (template == null) return;

            // show khi có item
            if (group) SetVisible(true);

            while (items.Count >= maxItems)
            {
                var old = items.Dequeue();
                if (old) Destroy(old.gameObject);
            }

            var inst = Instantiate(template, template.transform.parent);
            inst.text = msg;
            inst.gameObject.SetActive(true);
            items.Enqueue(inst);
            StartCoroutine(FadeAndDestroy(inst));
        }

        private System.Collections.IEnumerator FadeAndDestroy(TextMeshProUGUI t)
        {
            yield return new WaitForSeconds(lifeTime);

            // fade 0.4s
            float t0 = 0f, dur = 0.4f;
            Color c = t.color;
            while (t0 < dur && t != null)
            {
                t0 += Time.deltaTime;
                float a = Mathf.Lerp(1f, 0f, t0 / dur);
                t.color = new Color(c.r, c.g, c.b, a);
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);

            // nếu hết item → ẩn
            if (items.Count == 0 && group)
            {
                if (hideCo != null) StopCoroutine(hideCo);
                hideCo = StartCoroutine(HideAfterDelay());
            }
        }

        private System.Collections.IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(hideDelay);
            if (items.Count == 0) SetVisible(false);
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
            // tween tay 0.2s
            StartCoroutine(Fade(on ? 1f : 0f));
            group.interactable = on;
            group.blocksRaycasts = on;
        }

        private System.Collections.IEnumerator Fade(float target)
        {
            float dur = 0.2f;
            float t0 = 0f;
            float start = group.alpha;
            while (t0 < dur)
            {
                t0 += Time.deltaTime;
                group.alpha = Mathf.Lerp(start, target, t0 / dur);
                yield return null;
            }
            group.alpha = target;
        }
    }
}
