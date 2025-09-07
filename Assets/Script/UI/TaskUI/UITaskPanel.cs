using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wargency.Gameplay;

namespace Wargency.UI
{
    // Panel hiển thị 1 task trên UI (View thuần)
    // - Không tự trigger VFX khi hoàn thành; UITaskBoardController sẽ gọi PlayEffect(...)
    // - Có anchor an toàn cho VFX trong UI, kèm tùy chọn "normalize" để hiệu ứng không bị quá to
    [DisallowMultipleComponent]
    public class UITaskPanel : MonoBehaviour
    {
        [Header("UI Refs")]
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI requiredRoleText;
        public TextMeshProUGUI assigneeText;
        public TextMeshProUGUI roleCountText; // update 2908: hiển thị 0/1 hoặc 1/1
        public Slider progressBar;
        [SerializeField] private GameObject warningIconSmall;

        [Header("Gameplay")]
        [SerializeField] private TaskManager taskManager; // không bắt event ở đây nữa

        [Header("Smooth Progress")]
        public bool smoothProgress = true;
        [Tooltip("Giá trị/giây. 6–10 khá mượt")]
        public float smoothSpeed = 8f;
        private float visualProgress = 0f;

        // ===== Effects Anchor & Auto-size =====
        [Header("Effects Anchor & Auto-size")]
        [SerializeField] private RectTransform effectAnchor;                    // UPDATE 2025-09-05: anchor cho VFX
        [SerializeField] private float autoDestroyEffectAfter = 1f;
        [SerializeField] private bool compensateParentScale = true;
        [SerializeField] private Vector2 effectPixelSize = new(240, 240);      // GIẢM size mặc định một chút
        public RectTransform EffectAnchor => effectAnchor;

        // ===== Particle Normalize (để VFX không quá to trong UI) =====
        [Header("Effect Normalize (UI)")]
        [Tooltip("Bật để tự co nhỏ particle khi spawn vào UI")]
        [SerializeField] private bool normalizeParticleForUI = true;           // UPDATE 2025-09-05
        [Tooltip("Nhân vào Start Size/Speed/Shape Radius/Scale của tất cả ParticleSystem con")]
        [SerializeField, Range(0.05f, 3f)] private float particleScaleMultiplier = 0.35f; // 0.35 = nhỏ lại còn ~35%
        [Tooltip("Ép Particle dùng Local để scale ổn định trong UI")]
        [SerializeField] private bool forceLocalScalingMode = true;
        [Tooltip("Áp multiplier vào Shape.radius & Shape.scale (nếu bật)")]
        [SerializeField] private bool applyToShape = true;

        [Header("Spawn In Animation")]
        [SerializeField] private CanvasGroup cg;
        [SerializeField] private RectTransform rt;
        [SerializeField] private float spawnDuration = 0.18f;
        [SerializeField] private Vector2 spawnOffset = new(0f, -18f);

        // Trạng thái ràng buộc dữ liệu
        private TaskInstance lastBound;
        public TaskInstance Current { get; private set; }
        private bool playedSpawnAnim;

        private void Awake()
        {
            if (!rt) rt = transform as RectTransform;
            if (!cg) cg = GetComponent<CanvasGroup>();
            EnsureEffectAnchor();

            Current = null;
            lastBound = null;
            visualProgress = 0f;
        }

        private void OnEnable()
        {
            EnsureEffectAnchor();
            playedSpawnAnim = false;

            // Làm sạch UI progress khi vừa bật (an toàn khi dùng pool)
            if (progressBar) progressBar.value = 0f;
            visualProgress = 0f;
        }

        private void OnDisable()
        {
            // Reset nhẹ để lần reuse sau không mang “bóng ma” dữ liệu cũ
            Current = null;
            lastBound = null;
            visualProgress = 0f;
            if (progressBar) progressBar.value = 0f;
        }

        private void Update()
        {
            // Panel KHÔNG tự phát VFX khi progress == 1 nữa.
            float target = (Current != null) ? Current.progress01 : 0f;

            if (!smoothProgress)
            {
                if (progressBar) progressBar.value = target;
            }
            else if (progressBar && !Mathf.Approximately(visualProgress, target))
            {
                visualProgress = Mathf.MoveTowards(visualProgress, target, smoothSpeed * Time.deltaTime);
                progressBar.value = visualProgress;
            }

            // Spawn-in anim 1 lần khi enable
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

            // Ẩn text dài để tránh đè
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

            RefreshAssignee();

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

            bool needWarn = false;
            if (def != null && def.HasRoleRestriction(out var reqRole))
            {
                needWarn = (a == null) || (a.Role != reqRole);
            }
            if (warningIconSmall) warningIconSmall.SetActive(needWarn);
        }

        // ====== Effect API (để Board gọi) ======
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

            // UPDATE 2025-09-05: co nhỏ particle nếu cần
            if (normalizeParticleForUI)
                NormalizeParticlesForUI(go);

            var ps = go.GetComponentInChildren<ParticleSystem>();
            if (ps != null && !ps.main.playOnAwake) ps.Play();

            if (autoDestroyEffectAfter > 0f) Destroy(go, autoDestroyEffectAfter);
        }

        private void NormalizeParticlesForUI(GameObject root)
        {
            var systems = root.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                var main = ps.main;
                if (forceLocalScalingMode) main.scalingMode = ParticleSystemScalingMode.Local;

                // Nhân nhỏ kích thước & tốc độ để hợp UI
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

        // ===== Ensure / Create Anchor =====
        private void EnsureEffectAnchor()
        {
            if (effectAnchor) return;

            // 1) Ưu tiên marker component
            var marker = GetComponentInChildren<UIEffectAnchor>(true);
            if (marker && marker.transform is RectTransform rtFromMarker)
            {
                effectAnchor = rtFromMarker;
                return;
            }

            // 2) Tìm child tên "EffectAnchor"
            var t = transform.Find("EffectAnchor");
            if (t is RectTransform rtFromName)
            {
                effectAnchor = rtFromName;
                return;
            }

            // 3) Không có thì tự tạo 1 node
            var go = new GameObject("EffectAnchor", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = effectPixelSize;     // để particle UI dễ nhìn
            effectAnchor = rt;
        }
    }
}
