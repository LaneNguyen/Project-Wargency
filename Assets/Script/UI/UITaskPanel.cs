using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wargency.Gameplay;

namespace Wargency.UI
{
    /// Hiển thị task + phát effect UI (M3.4)
    /// GẮN LÊN PREFAB panel nhỏ của Task
    public class UITaskPanel : MonoBehaviour
    {
        [Header("Refs")]
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI requiredRoleText;
        public TextMeshProUGUI assigneeText;
        public Slider progressBar;

        [Header("Gameplay")]
        [SerializeField] private TaskManager taskManager;

        [Header("Animation slider mượt tí")]
        public bool smoothProgress = true;
        [Tooltip("Giá trị/giây. Gợi ý: 6–8 mượt, dễ nhìn")]
        public float smoothSpeed = 4f;
        private float visualProgress = 0f;

        [Header("Effects")]
        [SerializeField] private RectTransform effectAnchor; // auto-rebind nếu bị null
        [SerializeField, Tooltip("Tự hủy effect instance sau (giây). 0 = để prefab tự quản lý")]
        private float autoDestroyEffectAfter = 1f;
        [Header("Effects Scale Control")]
        [SerializeField] private bool compensateParentScale = true;
        [SerializeField] private Vector2 effectPixelSize = new Vector2(300, 300);

        private TaskInstance lastBound;
        public TaskInstance Current { get; private set; }
        public RectTransform EffectAnchor => effectAnchor;

        // ====== Lifecycle ======
        private void Awake() => EnsureEffectAnchor();
        private void OnEnable() => EnsureEffectAnchor();

        // Phòng khi panel bị thay đổi parent do pooling → rebind lại anchor
        private void OnTransformParentChanged() => EnsureEffectAnchor();

        // Tìm lại anchor theo thứ tự ưu tiên:
        // 1) Child có marker UIEffectAnchor
        // 2) Child tên "EffectAnchor" (nếu bạn thích đặt convention tên)
        // 3) Fallback: chính RectTransform của panel
        private void EnsureEffectAnchor()
        {
            if (effectAnchor != null) return;

            // 1) Marker
            var marker = GetComponentInChildren<UIEffectAnchor>(true);
            if (marker != null && marker.transform is RectTransform rtFromMarker)
            {
                effectAnchor = rtFromMarker;
                return;
            }

            // 2) Tên cố định (tùy chọn – an toàn khi không thêm marker)
            var t = transform.Find("EffectAnchor");
            if (t != null && t is RectTransform rtFromName)
            {
                effectAnchor = rtFromName;
                return;
            }

            // 3) Fallback
            effectAnchor = transform as RectTransform;
        }

        // ====== Data binding ======
        public void SetData(TaskInstance inst)
        {
            Current = inst;
            var def = (inst != null) ? inst.Definition : null;

            if (nameText != null)
                nameText.text = (def != null) ? def.DisplayName : "(Task)";

            if (requiredRoleText != null)
            {
                if (def != null && def.HasRoleRestriction(out var role))
                {
                    requiredRoleText.gameObject.SetActive(true);
                    requiredRoleText.text = $"Cần: {role}";
                }
                else requiredRoleText.gameObject.SetActive(false);
            }

            if (assigneeText != null)
            {
                if (inst != null && inst.Assignee != null)
                {
                    assigneeText.gameObject.SetActive(true);
                    assigneeText.text = $"{inst.Assignee.DisplayName} nhận task!";
                }
                else assigneeText.gameObject.SetActive(false);
            }

            if (!ReferenceEquals(lastBound, inst))
            {
                lastBound = inst;
                visualProgress = (inst != null) ? inst.progress01 : 0f;
                if (progressBar != null) progressBar.value = visualProgress;
            }
        }


        /// <summary>
        /// Làm mới nhanh toàn bộ những phần dễ thay đổi.
        /// Hiện tại chỉ refresh người nhận task để sửa lỗi compile nơi khác gọi.
        /// </summary>
        public void RefreshAll()
        {
            RefreshAssignee();
        }

        // ====== Effect spawning ======
        public void PlayEffect(GameObject effectPrefab)
        {
            if (effectPrefab == null) return;

            EnsureEffectAnchor();
            var parent = (Transform)(effectAnchor != null ? effectAnchor : transform);

            var go = Instantiate(effectPrefab, parent, false);

            // Chuẩn hoá transform local
            if (go.transform is RectTransform rt)
            {
                rt.anchoredPosition = Vector2.zero;
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one;

                // (tuỳ chọn) đặt size tuyệt đối để nhất quán về pixel
                if (effectPixelSize.x > 0 && effectPixelSize.y > 0)
                    rt.sizeDelta = effectPixelSize;
            }
            else
            {
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
            }

            // Bù tỉ lệ cha để VFX không bị "bé xíu" khi panel bị scale nhỏ
            if (compensateParentScale)
            {
                var ls = parent.lossyScale;
                var inv = new Vector3(
                    ls.x != 0f ? 1f / ls.x : 1f,
                    ls.y != 0f ? 1f / ls.y : 1f,
                    ls.z != 0f ? 1f / ls.z : 1f
                );

                // Áp scale bù trên root của effect
                if (go.transform is RectTransform)
                    go.transform.localScale = Vector3.Scale(go.transform.localScale, inv);
                else
                    go.transform.localScale = Vector3.Scale(go.transform.localScale, inv);
            }

            // Play particle nếu cần
            var ps = go.GetComponentInChildren<ParticleSystem>();
            if (ps != null && !ps.main.playOnAwake) ps.Play();

            if (autoDestroyEffectAfter > 0f)
                Destroy(go, autoDestroyEffectAfter);
        }

        /// <summary>
        /// Cập nhật phần hiển thị người được gán (Assignee) cho task.
        /// Dễ hiểu: nếu có người → hiện tên + icon (nếu bạn muốn sau này).
        /// Nếu chưa ai nhận → ghi "<Unassigned>" và ẩn dòng này cho gọn.
        /// </summary>
        public void RefreshAssignee()
        {
            if (Current == null) return;

            var a = Current.Assignee;

            if (assigneeText != null)
            {
                if (a != null)
                {
                    assigneeText.gameObject.SetActive(true);
                    assigneeText.text = $"{a.DisplayName} nhận task!";
                }
                else
                {
                    // Chưa có ai nhận
                    assigneeText.text = "<Unassigned>";
                    assigneeText.gameObject.SetActive(false);
                }
            }
        }

        private void Update()
        {
            if (progressBar == null) return;

            float target = (Current != null) ? Current.progress01 : 0f;

            if (!smoothProgress)
            {
                progressBar.value = target;
                return;
            }

            if (!Mathf.Approximately(visualProgress, target))
            {
                visualProgress = Mathf.MoveTowards(visualProgress, target, smoothSpeed * Time.deltaTime);
                progressBar.value = visualProgress;
            }
        }
    }
}
