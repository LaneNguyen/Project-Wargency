using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wargency.Gameplay;

// panel này đại diện cho 1 task trên UI
// hiện tên, role cần, ai nhận, progress bar chạy mượt
// có hiệu ứng spawn in và icon cảnh báo khi role sai
// UI => Manager => Gameplay truyền dữ liệu task vào

namespace Wargency.UI
{
    [DisallowMultipleComponent]
    public class UITaskPanel : MonoBehaviour
    {
        [Header("UI Refs")]
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI requiredRoleText;
        public TextMeshProUGUI assigneeText;
        public TextMeshProUGUI roleCountText; //update 2908: hiển thị 0/1 hoặc 1/1
        public Slider progressBar;
        [SerializeField] private GameObject warningIconSmall;   // icon nhỏ khi thiếu/mismatch role

        [Header("Gameplay")]
        [SerializeField] private TaskManager taskManager;

        [Header("Smooth Progress")]
        public bool smoothProgress = true;
        [Tooltip("Giá trị/giây. 6–10 khá mượt")]
        public float smoothSpeed = 8f;
        private float visualProgress = 0f;

        [Header("Effects Anchor & Auto-size")]
        [SerializeField] private RectTransform effectAnchor;
        [SerializeField] private float autoDestroyEffectAfter = 1f;
        [SerializeField] private bool compensateParentScale = true;
        [SerializeField] private Vector2 effectPixelSize = new(300, 300);

        [Header("Spawn In Animation")]
        [SerializeField] private CanvasGroup cg;        // NEW: CanvasGroup để fade
        [SerializeField] private RectTransform rt;      // cache
        [SerializeField] private float spawnDuration = 0.18f;
        [SerializeField] private Vector2 spawnOffset = new(0f, -18f);


        private TaskInstance lastBound;
        public TaskInstance Current { get; private set; }
        public RectTransform EffectAnchor => effectAnchor;
        private bool playedSpawnAnim;

        private void Awake()
        {
            if (!rt) rt = transform as RectTransform;
            if (!cg) cg = GetComponent<CanvasGroup>();
            EnsureEffectAnchor();
        }

        private void OnEnable()
        {
            EnsureEffectAnchor();
            // panel lấy từ pool -> chạy spawn anim nhẹ
            playedSpawnAnim = false;
        }

        private void Update()
        {
            if (progressBar == null) return;
            float target = (Current != null) ? Current.progress01 : 0f;

            if (!smoothProgress)
            {
                progressBar.value = target;
            }
            else if (!Mathf.Approximately(visualProgress, target))
            {
                visualProgress = Mathf.MoveTowards(visualProgress, target, smoothSpeed * Time.deltaTime);
                progressBar.value = visualProgress;
            }

            // Lần đầu xuất hiện sau SetActive(true) -> chạy anim 1 lần
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
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / spawnDuration);
                // easeOutQuad
                float e = 1f - (1f - p) * (1f - p);
                cg.alpha = e;
                rt.anchoredPosition = Vector2.LerpUnclamped(from, origin, e);
                yield return null;
            }
            cg.alpha = 1f;
            rt.anchoredPosition = origin;
        }

        // ====== Data binding ======
        public void SetData(TaskInstance inst)
        {
            Current = inst;
            var def = inst != null ? inst.Definition : null;

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

            // Optional - ẩn text dài để tránh đè tùm lum la
            if (requiredRoleText) requiredRoleText.gameObject.SetActive(false);
            if (assigneeText) assigneeText.gameObject.SetActive(false);

            if (nameText) nameText.text = (def != null) ? def.DisplayName : "(Task)";

            // Required role
            if (requiredRoleText)
            {
                if (def != null && def.HasRoleRestriction(out var role))
                {
                    requiredRoleText.gameObject.SetActive(true);
                    requiredRoleText.text = $"Cần: {role}";
                }
                else requiredRoleText.gameObject.SetActive(false);
            }

            RefreshAssignee(); // sẽ tự xử lý warning icon

            if (!ReferenceEquals(lastBound, inst))
            {
                lastBound = inst;
                visualProgress = (inst != null) ? inst.progress01 : 0f;
                if (progressBar) progressBar.value = visualProgress;
            }
        }

        // Gọi lại khi reassign/assign
        public void RefreshAssignee()
        {
            if (Current == null) return;
            var def = Current.Definition;
            var a = Current.Assignee;

            if (roleCountText && Current != null)
            {
                def = Current.Definition;
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

            // Hiện icon cảnh báo khi:
            // - Task yêu cầu Role, và (chưa ai nhận) hoặc (agent hiện tại không đúng role)
            bool needWarn = false;
            if (def != null && def.HasRoleRestriction(out var reqRole))
            {
                needWarn = (a == null) || (a.Role != reqRole);
            }
            if (warningIconSmall) warningIconSmall.SetActive(needWarn);
        }

        // ====== Effect spawning (giữ y như bản cũ, thêm anchor safe) ======
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
                var inv = new Vector3(ls.x != 0f ? 1f / ls.x : 1f, ls.y != 0f ? 1f / ls.y : 1f, ls.z != 0f ? 1f / ls.z : 1f);
                go.transform.localScale = Vector3.Scale(go.transform.localScale, inv);
            }

            var ps = go.GetComponentInChildren<ParticleSystem>();
            if (ps != null && !ps.main.playOnAwake) ps.Play();

            if (autoDestroyEffectAfter > 0f) Destroy(go, autoDestroyEffectAfter);
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
            effectAnchor = transform as RectTransform;
        }


    }
}
