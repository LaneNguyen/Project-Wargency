using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wargency.Gameplay;
using static UnityEngine.Rendering.DebugUI;

namespace Wargency.UI
{
    [DisallowMultipleComponent]
    public class UITaskPanel : MonoBehaviour
    {
        [Header("UI Refs")]
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI requiredRoleText;
        public TextMeshProUGUI assigneeText;
        public TextMeshProUGUI roleCountText;
        public Slider progressBar;

        [Header("Money Gain UI")] // 0909 Update
        [SerializeField] private TextMeshProUGUI moneyGainPrefab; // text nhỏ đặt sẵn trong TaskPanel
        [SerializeField] private float moneyTextDuration = 1.2f;
        [SerializeField] private Vector2 moneyTextOffset = new Vector2(0f, 50f);

        [SerializeField] private GameObject warningIconSmall; // sẽ auto-wire nếu để trống

        [Header("Smooth Progress")]
        public bool smoothProgress = true;
        [SerializeField] private float smoothDuration = 0.3f; // thời gian để đi từ current => target

        private float visualProgress = 0f;
        private float tweenTime = 0f;
        private float fromValue = 0f;
        private float toValue = 0f;

        [Header("Effects Anchor & Auto-size")]
        [SerializeField] private RectTransform effectAnchor;
        [SerializeField] private float autoDestroyEffectAfter = 1f;
        [SerializeField] private bool compensateParentScale = true;
        [SerializeField] private Vector2 effectPixelSize = new(240, 240);
        public RectTransform EffectAnchor => effectAnchor;

        [Header("Effect Normalize (UI)")]
        [SerializeField] private bool normalizeParticleForUI = true;
        [SerializeField, Range(0.05f, 3f)] private float particleScaleMultiplier = 0.35f;
        [SerializeField] private bool forceLocalScalingMode = true;
        [SerializeField] private bool applyToShape = true;

        [Header("Spawn In Animation")]
        [SerializeField] private CanvasGroup cg;
        [SerializeField] private RectTransform rt;
        [SerializeField] private float spawnDuration = 0.18f;
        [SerializeField] private Vector2 spawnOffset = new(0f, -18f);

        [Header("Warning Icon (UI Pixel Perfect)")]
        [SerializeField] private Vector2 warningIconPixelSize = new(56, 56);
        [SerializeField] private bool warningIconCompensateParentScale = true;
        [SerializeField] private Vector2 warningIconOffset = new(-8f, -8f); // top-right margin

        // Blink cảnh báo
        private Coroutine warnBlinkCo;
        [SerializeField] private float warnBlinkDuration = 0.9f;
        [SerializeField] private float warnBlinkFreq = 12f;

        private TaskInstance lastBound;
        public TaskInstance Current { get; private set; }
        private bool playedSpawnAnim;

        private void Awake()
        {
            if (!rt) rt = transform as RectTransform;
            if (!cg) cg = GetComponent<CanvasGroup>();
            EnsureEffectAnchor();
            if (moneyGainPrefab) moneyGainPrefab.gameObject.SetActive(false);
            AutoWireWarningIcon(); // tự tìm icon nếu quên gán
        }

        private void OnEnable()
        {
            EnsureEffectAnchor();
            AutoWireWarningIcon();

            playedSpawnAnim = false;
            if (progressBar) progressBar.value = 0f;
            visualProgress = 0f;

            if (moneyGainPrefab) moneyGainPrefab.gameObject.SetActive(false);
            // Mặc định ẩn icon
            if (warningIconSmall) warningIconSmall.SetActive(false);
        }

        private void OnDisable()
        {
            Current = null;
            lastBound = null;
            visualProgress = 0f;
            if (progressBar) progressBar.value = 0f;
            if (lastBound != null)
                lastBound.OnCompletedWithRewards -= HandleTaskCompletedUI;// 0909 Update
            if (warnBlinkCo != null) { StopCoroutine(warnBlinkCo); warnBlinkCo = null; }
            if (warningIconSmall) warningIconSmall.SetActive(false);
        }

        private float SmoothStep01(float edge0, float edge1, float x)
        {
            x = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return x * x * (3f - 2f * x);
        }
        private void Update()
        {
            float target = (Current != null) ? Current.progress01 : 0f;

            

            if (smoothProgress)
            {
                // nếu target đổi => reset tween
                if (!Mathf.Approximately(toValue, target))
                {
                    fromValue = visualProgress;
                    toValue = target;
                    tweenTime = 0f;
                }
                
                // tăng thời gian tween
                tweenTime += Time.deltaTime;
                float t = Mathf.Clamp01(tweenTime / Mathf.Max(0.0001f, smoothDuration));

                visualProgress = Mathf.Lerp(fromValue, toValue, SmoothStep01(0f, 1f, t));
                progressBar.value = visualProgress;
            }
            else
            {
                // set cứng 
                visualProgress = target;
                progressBar.value = target;
            }

            if (!playedSpawnAnim && cg != null && rt != null)
            {
                playedSpawnAnim = true;
                StopAllCoroutines();
                StartCoroutine(SpawnInCo());
            }
        }

        private System.Collections.IEnumerator SpawnInCo()
        {
            if (cg == null || rt == null) yield break;
            var origin = rt.anchoredPosition;
            var from = origin + spawnOffset;
            float t = 0f;
            cg.alpha = 0f;
            rt.anchoredPosition = from;

            while (t < spawnDuration)
            {
                t += (spawnDuration <= 0f ? 1f : Time.unscaledDeltaTime);
                float p = Mathf.Clamp01(t / Mathf.Max(0.0001f, spawnDuration));
                float e = 1f - (1f - p) * (1f - p);
                cg.alpha = e;
                rt.anchoredPosition = Vector2.LerpUnclamped(from, origin, e);
                yield return null;
            }
            cg.alpha = 1f;
            rt.anchoredPosition = origin;
        }

        public void SetData(TaskInstance inst)
        {
            Current = inst;
            var def = inst != null ? inst.Definition : null;
            if (lastBound != null)
                lastBound.OnCompletedWithRewards -= HandleTaskCompletedUI; // 0909 Update

            Current = inst;

            if (Current != null)
                Current.OnCompletedWithRewards += HandleTaskCompletedUI;    // 0909 Update

            lastBound = inst;

            if (roleCountText)
            {
                int required = (def != null && def.UseRequiredRole) ? 1 : 0;
                int assigned = 0;
                if (inst != null && inst.Assignee != null)
                {
                    assigned = (!def || !def.UseRequiredRole) ? 1 :
                               (inst.Assignee.Role == def.RequiredRole ? 1 : 0);
                }
                roleCountText.text = $"{assigned}/{required}";
            }

            if (requiredRoleText) requiredRoleText.gameObject.SetActive(false);
            if (assigneeText) assigneeText.gameObject.SetActive(false);
            if (nameText) nameText.text = (def != null) ? def.DisplayName : "(Task)";

            if (requiredRoleText)
            {
                if (def != null && def.HasRoleRestriction(out var role))
                {
                    requiredRoleText.gameObject.SetActive(true);
                    requiredRoleText.text = $"Cần: {role}";
                }
                else requiredRoleText.gameObject.SetActive(false);
            }

            RefreshAssignee();

            if (!ReferenceEquals(lastBound, inst))
            {
                lastBound = inst;
                visualProgress = (inst != null) ? inst.progress01 : 0f;
                if (progressBar) progressBar.value = visualProgress;
            }
        }

        public void RefreshAssignee()
        {
            if (Current == null) return;
            var def = Current.Definition;
            var a = Current.Assignee;

            if (roleCountText && Current != null)
            {
                int required = (def != null && def.UseRequiredRole) ? 1 : 0;
                int assigned = 0;
                if (Current.Assignee != null)
                {
                    assigned = (!def || !def.UseRequiredRole) ? 1 :
                               (Current.Assignee.Role == def.RequiredRole ? 1 : 0);
                }
                roleCountText.text = $"{assigned}/{required}";
            }

            if (assigneeText)
            {
                if (a != null)
                {
                    assigneeText.gameObject.SetActive(true);
                    assigneeText.text = $"{a.DisplayName} nhận task!";
                }
                else
                {
                    assigneeText.text = "<Unassigned>";
                    assigneeText.gameObject.SetActive(false);
                }
            }

            // KHÔNG bật warning khi chưa có assignee
            if (warningIconSmall) warningIconSmall.SetActive(false);
        }

        // === API: gọi khi drop sai role / assign fail ===

        public void ShowMismatchWarning(float duration = 0.9f)
        {
            AutoWireWarningIcon();
            if (!warningIconSmall) return;

            // --- ÁP THAM SỐ TỪ INSPECTOR ---
            var rt = warningIconSmall.GetComponent<RectTransform>();
            if (rt)
            {
                // Bám theo CanvasScaler: sizePx (pixel thật) / scaleFactor -> sizeDelta UI
                var canvas = GetComponentInParent<Canvas>();
                float sf = canvas ? canvas.scaleFactor : 1f;
                if (sf <= 0f) sf = 1f;

                rt.sizeDelta = new Vector2(
                    warningIconPixelSize.x / sf,
                    warningIconPixelSize.y / sf
                );

                // Đặt góc phải–trên + offset
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = warningIconOffset;
            }

            // Bù scale của cha để icon nhìn đúng kích thước dù panel bị thu nhỏ
            if (warningIconCompensateParentScale)
            {
                var ls = warningIconSmall.transform.lossyScale;
                var inv = new Vector3(
                    ls.x != 0 ? 1f / ls.x : 1f,
                    ls.y != 0 ? 1f / ls.y : 1f,
                    ls.z != 0 ? 1f / ls.z : 1f
                );
                warningIconSmall.transform.localScale = inv;
            }
            else warningIconSmall.transform.localScale = Vector3.one;

            // Không để LayoutGroup ép size
            var le = warningIconSmall.GetComponent<LayoutElement>() ?? warningIconSmall.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            warningIconSmall.transform.SetAsLastSibling();
            warningIconSmall.SetActive(true);

            if (warnBlinkCo != null) StopCoroutine(warnBlinkCo);
            warnBlinkCo = StartCoroutine(WarnBlinkCo(duration));
        }



        private void NormalizeWarningIconForUIPixels()
        {
            if (!warningIconSmall) return;
            var rt = warningIconSmall.GetComponent<RectTransform>();
            if (rt) rt.sizeDelta = warningIconPixelSize;

            // Ngăn LayoutGroup ép size icon
            var le = warningIconSmall.GetComponent<LayoutElement>();
            if (!le) le = warningIconSmall.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            // Bù scale cha để icon luôn đúng kích thước trên màn hình
            if (warningIconCompensateParentScale)
            {
                var ls = warningIconSmall.transform.lossyScale;
                var inv = new Vector3(
                    ls.x != 0 ? 1f / ls.x : 1f,
                    ls.y != 0 ? 1f / ls.y : 1f,
                    ls.z != 0 ? 1f / ls.z : 1f
                );
                warningIconSmall.transform.localScale = inv;
            }
        }

        public void HideWarning()
        {
            if (warnBlinkCo != null) { StopCoroutine(warnBlinkCo); warnBlinkCo = null; }
            if (warningIconSmall) warningIconSmall.SetActive(false);
        }

        private System.Collections.IEnumerator WarnBlinkCo(float dur)
        {
            float t = 0f;
            var cgWarn = warningIconSmall.GetComponent<CanvasGroup>();
            if (cgWarn == null) cgWarn = warningIconSmall.AddComponent<CanvasGroup>();

            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                cgWarn.alpha = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * warnBlinkFreq);
                yield return null;
            }
            cgWarn.alpha = 1f;
            warnBlinkCo = null;
        }

        // 0909 Update: callback hiển thị +money
        private void HandleTaskCompletedUI(TaskInstance t, int money, int score)
        {
            if (money > 0)
                ShowMoneyGain(money); // dùng hiệu ứng nổi + fade
        }


        // ==== Effect helpers ====
        public void PlayEffect(GameObject effectPrefab)
        {
            if (!effectPrefab) return;
            EnsureEffectAnchor();
            var parent = (Transform)(effectAnchor ? effectAnchor : transform);
            var go = Instantiate(effectPrefab, parent, false);

            if (go.transform is RectTransform ert)
            {
                ert.anchoredPosition = Vector2.zero;
                ert.localRotation = Quaternion.identity;
                ert.localScale = Vector3.one;
                if (effectPixelSize.x > 0 && effectPixelSize.y > 0) ert.sizeDelta = effectPixelSize;
            }
            else
            {
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
            }

            if (compensateParentScale)
            {
                var ls = parent.lossyScale;
                var inv = new Vector3(
                    ls.x != 0f ? 1f / ls.x : 1f,
                    ls.y != 0f ? 1f / ls.y : 1f,
                    ls.z != 0f ? 1f / ls.z : 1f
                );
                go.transform.localScale = Vector3.Scale(go.transform.localScale, inv);
            }

            if (normalizeParticleForUI) NormalizeParticlesForUI(go);

            var ps = go.GetComponentInChildren<ParticleSystem>();
            if (ps != null && !ps.main.playOnAwake) ps.Play();

            if (autoDestroyEffectAfter > 0f) Destroy(go, autoDestroyEffectAfter);
        }

        private void NormalizeParticlesForUI(GameObject root)
        {
            var systems = root.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in systems)
            {
                var main = ps.main;
                if (forceLocalScalingMode) main.scalingMode = ParticleSystemScalingMode.Local;

                main.startSizeMultiplier *= particleScaleMultiplier;
                main.startSpeedMultiplier *= particleScaleMultiplier;

                if (applyToShape)
                {
                    var shape = ps.shape;
                    if (shape.enabled)
                    {
                        shape.radius *= particleScaleMultiplier;
                        shape.scale = shape.scale * particleScaleMultiplier;
                    }
                }
            }
        }

        private void OnTransformParentChanged() => EnsureEffectAnchor();

        private void EnsureEffectAnchor()
        {
            if (effectAnchor) return;

            var marker = GetComponentInChildren<UIEffectAnchor>(true);
            if (marker && marker.transform is RectTransform rtFromMarker)
            {
                effectAnchor = rtFromMarker;
                return;
            }

            var t = transform.Find("EffectAnchor");
            if (t is RectTransform rtFromName)
            {
                effectAnchor = rtFromName;
                return;
            }

            var go = new GameObject("EffectAnchor", typeof(RectTransform));
            var rtEA = go.GetComponent<RectTransform>();
            rtEA.SetParent(transform, false);
            rtEA.anchorMin = new Vector2(0.5f, 0.5f);
            rtEA.anchorMax = new Vector2(0.5f, 0.5f);
            rtEA.pivot = new Vector2(0.5f, 0.5f);
            rtEA.anchoredPosition = Vector2.zero;
            rtEA.sizeDelta = effectPixelSize;
            effectAnchor = rtEA;
        }

        // === Auto-wire warning icon nếu quên gán ===
        private void AutoWireWarningIcon()
        {
            if (warningIconSmall) return;

            // 1) tìm đúng tên con phổ biến
            var t = transform.Find("WarningIcon");
            if (!t) t = transform.Find("WarningIconSmall");
            if (!t)
            {
                // 2) fallback: tìm theo tên chứa "warn"
                foreach (var img in GetComponentsInChildren<Image>(true))
                {
                    if (img && img.name.ToLower().Contains("warn"))
                    {
                        t = img.transform;
                        break;
                    }
                }
            }
            if (t) warningIconSmall = t.gameObject;
        }

        // === 0909 Update: API gọi khi kiếm được tiền từ task ===
        public void ShowMoneyGain(int amount)
        {

            if (moneyGainPrefab)
            {
                var txt = moneyGainPrefab;
                txt.text = $"+{amount}$";

                // đặt parent anchor
                var parent = (RectTransform)(effectAnchor ? effectAnchor : (RectTransform)transform);
                var rt = txt.rectTransform;
                rt.SetParent(parent, worldPositionStays: false);

                // reset trạng thái + đặt vị trí offset
                var cg = txt.GetComponent<CanvasGroup>();
                if (!cg) cg = txt.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 1f;

                rt.anchoredPosition = moneyTextOffset;
                txt.gameObject.SetActive(true);

                StartCoroutine(MoneyGainCo(txt));
                return;
            }
           
        }

        private System.Collections.IEnumerator MoneyGainCo(TextMeshProUGUI txt)
        {
            float t = 0f;
            var cg = txt.GetComponent<CanvasGroup>();
            if (!cg) cg = txt.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 1f;

            Vector2 startPos = txt.rectTransform.anchoredPosition;
            Vector2 endPos = startPos + new Vector2(0f, 20f);

            while (t < moneyTextDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = t / moneyTextDuration;
                cg.alpha = 1f - p;
                txt.rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, p);
                yield return null;
            }

            Destroy(txt.gameObject);
        }
    }
}
