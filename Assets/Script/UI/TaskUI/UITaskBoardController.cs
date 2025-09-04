using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Wargency.Gameplay;

// quản lý cả bảng task luôn á, gồm pool, filter sort và mấy hiệu ứng
// nghe TaskManager đổi task thì update danh sách liền tay
// có slide in từ bên phải nhìn vui mắt khi task mới hiện ra
// UI => Manager => Gameplay liên tục nên test kỹ kẻo trễ nhịp

namespace Wargency.UI
{
    public class UITaskBoardController : MonoBehaviour
    {
        [Header("Refs")]
        public TaskManager taskManager;
        public RectTransform panelContainer;
        public UITaskPanel itemPrefab;

        [Header("Optional UI")]
        public GameObject noTasksPlaceholder;       // hiển thị khi rỗng
        public TextMeshProUGUI listText;            // debug
        public Button spawnFirstButton;             // smoke test ONLY
        public Button startAllButton;               // smoke test ONLY

        [Header("Alerts (optional)")]
        public UILiveAlertsFeed alerts;             // đẩy ✅ ❌

        [Header("Effects")]
        [SerializeField] private GameObject effectCompletedPrefab;
        [SerializeField] private GameObject effectFailedPrefab;
        [SerializeField] private float effectLingerSeconds = 1f;

        [Header("UI Limit & Slide-in")]
        [Tooltip("Giới hạn SỐ Ô HIỂN THỊ ở UI (không phụ thuộc limit nội bộ của TaskManager). 0 = không giới hạn.")]
        [SerializeField] private int maxVisibleSlots = 0;

        [Tooltip("Khoảng offset X khi panel mới trượt vào từ phải.")]
        [SerializeField] private float slideInOffsetX = 220f;

        [Tooltip("Thời gian trượt vào (giây).")]
        [SerializeField] private float slideInDuration = 0.15f;

        [Tooltip("Tên child để animate (nếu rỗng sẽ animate chính RectTransform của panel).")]
        [SerializeField] private string slideContentChildName = "Content";

        // Mapping
        private readonly Dictionary<TaskInstance, UITaskPanel> taskToPanel = new();
        private readonly HashSet<UITaskPanel> panelsInEffect = new();
        private readonly List<UITaskPanel> pool = new();

        // --- Hooks mở rộng ---
        public System.Func<TaskInstance, bool> Filter;
        public System.Comparison<TaskInstance> Sort;
        public System.Func<TaskInstance, string> GroupKey;

        private static readonly List<TaskInstance> _tmp = new(64);

        // ===== NEW: theo dõi additions & vị trí trống sau khi complete =====
        private readonly HashSet<TaskInstance> _shownOnce = new();   // những task đã từng hiện
        private int _pendingVacancyIndex = -1;                       // slot index vừa trống do complete/failed

        private void Awake()
        {
            if (!taskManager) taskManager = FindFirstObjectByType<TaskManager>();
            if (spawnFirstButton) spawnFirstButton.onClick.AddListener(SpawnFirst);
            if (startAllButton) startAllButton.onClick.AddListener(StartAll);

            if (!panelContainer) Debug.LogWarning("[UITaskBoardController] panelContainer chưa được gán.");
            if (!itemPrefab) Debug.LogWarning("[UITaskBoardController] itemPrefab chưa được gán.");
        }

        private void OnEnable()
        {
            if (!taskManager) return;
            taskManager.OnTaskCompleted += HandleTaskCompleted;
            taskManager.OnTaskFailed += HandleTaskFailed;

            // Nếu TaskManager có OnTaskSpawned/Cancelled, có thể nghe ở đây để force refresh tức thời.
            // taskManager.OnTaskSpawned += _ => ForceRefresh(); ...
        }

        private void OnDisable()
        {
            if (!taskManager) return;
            taskManager.OnTaskCompleted -= HandleTaskCompleted;
            taskManager.OnTaskFailed -= HandleTaskFailed;
        }

        private void Update()
        {
            if (!taskManager) return;

            // 1) Lấy danh sách active
            var active = taskManager.Active; // IReadOnlyList<TaskInstance>

            // 2) Chuyển sang _tmp và áp Filter/Sort (tránh alloc mỗi frame), loại Completed/Failed
            _tmp.Clear();
            for (int i = 0; i < active.Count; i++)
            {
                var t = active[i];
                if (t.state == TaskInstance.TaskState.Completed || t.state == TaskInstance.TaskState.Failed) continue;
                if (Filter != null && !Filter(t)) continue;
                _tmp.Add(t);
            }
            if (Sort != null) _tmp.Sort(Sort);

            // 2.5) Áp giới hạn UI nếu cần
            int logicalCount = _tmp.Count;
            if (maxVisibleSlots > 0 && logicalCount > maxVisibleSlots)
                logicalCount = maxVisibleSlots;

            // 3) Số panel cần = task còn hiển thị (đã giới hạn) + panel đang playing effect
            int requiredCount = logicalCount + panelsInEffect.Count;
            EnsurePool(requiredCount);

            // 4) Bind
            int vi = 0;
            taskToPanel.Clear();

            // Mảng tạm giữ mapping slot index -> panel (để xác định siblingIndex)
            for (int i = 0; i < logicalCount; i++)
            {
                var inst = _tmp[i];

                // lấy một panel rảnh
                var item = pool[vi++];

                if (panelsInEffect.Contains(item))
                {
                    vi--; // trả slot
                    // tìm panel khác rảnh
                    bool found = false;
                    for (int seek = vi; seek < pool.Count; seek++)
                    {
                        if (!panelsInEffect.Contains(pool[seek]))
                        {
                            item = pool[seek];
                            pool[seek] = pool[vi];
                            pool[vi] = item;
                            found = true;
                            break;
                        }
                    }
                    if (!found) continue;
                    vi++;
                }

                if (!item.gameObject.activeSelf) item.gameObject.SetActive(true);

                // === Slide-in logic: task mới xuất hiện ===
                bool isNewAppearance = !_shownOnce.Contains(inst);
                int desiredSibling = i; // vị trí mục tiêu theo sort/filter

                // Nếu có slot trống do task vừa hoàn thành → đặt sibling vào đó, slide-in từ phải
                if (isNewAppearance && _pendingVacancyIndex >= 0)
                {
                    desiredSibling = Mathf.Clamp(_pendingVacancyIndex, 0, logicalCount - 1);
                    _pendingVacancyIndex = -1; // dùng xong reset
                }

                item.transform.SetSiblingIndex(desiredSibling);

                // Gán data trước để panel layout đúng kích thước
                item.SetData(inst);
                var dz = item.GetComponent<UITaskDropZone>();
                if (dz != null) dz.Bind(inst);

                taskToPanel[inst] = item;

                // Nếu là lần đầu xuất hiện → chạy slide-in
                if (isNewAppearance)
                {
                    _shownOnce.Add(inst);
                    PlaySlideInFromRight(item);
                }
            }

            // 5) Ẩn panel thừa (tránh đụng panel đang chạy effect)
            for (int j = vi; j < pool.Count; j++)
            {
                var p = pool[j];
                if (!panelsInEffect.Contains(p) && p.gameObject.activeSelf)
                    p.gameObject.SetActive(false);
            }

            // 6) Placeholder
            bool empty = (logicalCount == 0);
            if (noTasksPlaceholder) noTasksPlaceholder.SetActive(empty);

            // Nút Start All: sáng nếu có ít nhất một task startable
            if (startAllButton != null)
                startAllButton.interactable = HasAnyStartableTask();

            // Debug list
            if (listText) RenderDebugList(_tmp);
        }

        private void EnsurePool(int need)
        {
            while (pool.Count < need)
            {
                var item = Instantiate(itemPrefab, panelContainer);
                // Khuyến nghị: prefab chứa CanvasGroup để spawn-in animation hoạt động
                pool.Add(item);
            }
        }

        // ===== Handlers =====
        private void HandleTaskCompleted(TaskInstance inst)
        {
            if (inst == null) return;
            if (!taskToPanel.TryGetValue(inst, out var panel) || !panel) return;

            // Ghi nhận vị trí trống (sibling index) để task mới lấp vào
            _pendingVacancyIndex = panel.transform.GetSiblingIndex();

            if (alerts) alerts.Push($"✅ Hoàn thành: \"{inst.DisplayName}\"");
            StartCoroutine(PlayEffectAndRelease(panel, effectCompletedPrefab));
        }

        private void HandleTaskFailed(TaskInstance inst)
        {
            if (inst == null) return;
            if (!taskToPanel.TryGetValue(inst, out var panel) || !panel) return;

            // Ghi nhận vị trí trống cho case fail (cũng dùng slide-in)
            _pendingVacancyIndex = panel.transform.GetSiblingIndex();

            if (alerts) alerts.Push($"❌ Thất bại: \"{inst.DisplayName}\"");
            StartCoroutine(PlayEffectAndRelease(panel, effectFailedPrefab));
        }

        private System.Collections.IEnumerator PlayEffectAndRelease(UITaskPanel panel, GameObject effectPrefab)
        {
            if (!panel) yield break;
            panelsInEffect.Add(panel);

            panel.PlayEffect(effectPrefab);
            yield return new WaitForSeconds(effectLingerSeconds);

            panelsInEffect.Remove(panel);
            if (panel && panel.gameObject) panel.gameObject.SetActive(false);
        }

        // ===== Slide-in helper =====
        private void PlaySlideInFromRight(UITaskPanel panel)
        {
            if (!panel) return;

            // Tìm node để animate: ưu tiên child "Content", không có thì dùng chính rect của panel
            RectTransform animRT = null;
            if (!string.IsNullOrEmpty(slideContentChildName))
            {
                var tr = panel.transform.Find(slideContentChildName) as RectTransform;
                if (tr) animRT = tr;
            }
            if (!animRT) animRT = panel.transform as RectTransform;

            // đảm bảo có CanvasGroup để fade-in nhẹ (tùy chọn)
            var cg = panel.GetComponentInChildren<CanvasGroup>();
            if (!cg)
            {
                cg = panel.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
            }

            // Dừng coroutine cũ (nếu panel tái dùng)
            panel.StopAllCoroutines();
            panel.StartCoroutine(SlideInCoroutine(animRT, cg));
        }

        private System.Collections.IEnumerator SlideInCoroutine(RectTransform rt, CanvasGroup cg)
        {
            if (!rt) yield break;

            // giữ vị trí Y hiện tại (đã được Layout sắp), chỉ slide trục X
            Vector2 end = rt.anchoredPosition;
            Vector2 start = new Vector2(end.x + Mathf.Abs(slideInOffsetX), end.y);

            float t = 0f;
            rt.anchoredPosition = start;
            if (cg) cg.alpha = 0f;

            while (t < slideInDuration && rt != null)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / slideInDuration);
                u = 1f - Mathf.Pow(1f - u, 3f); // ease-out

                rt.anchoredPosition = Vector2.LerpUnclamped(start, end, u);
                if (cg) cg.alpha = Mathf.Lerp(0f, 1f, u);
                yield return null;
            }

            if (rt) rt.anchoredPosition = end;
            if (cg) cg.alpha = 1f;
        }

        // ===== Smoke test (tắt trên build) =====
        public void SpawnFirst()
        {
            if (taskManager == null) return;

            // Giới hạn UI (không bắt buộc) — tránh spam nhiều hơn khung hiển thị
            if (maxVisibleSlots > 0 && taskManager.Active.Count >= maxVisibleSlots)
            {
                Debug.Log("[UI] Đã đạt giới hạn hiển thị UI (maxVisibleSlots). Bỏ qua spawn smoke.");
                return;
            }

            var defs = taskManager.availableDefinitions;
            if (defs != null && defs.Length > 0 && defs[0] != null)
            {
                var inst = taskManager.Spawn(defs[0]); // New, chưa có assignee
                if (inst == null)
                    Debug.LogWarning("[UI] Spawn thất bại (đạt giới hạn concurrent tasks?)");
                // Người chơi sẽ kéo avatar vào panel để Assign, rồi bấm Start All để chạy.
            }
            else
            {
                Debug.LogWarning("[UI] Không tìm thấy availableDefinitions[0]");
            }
        }

        private void StartAll()
        {
            if (taskManager == null) return;

            int started = 0;
            foreach (var t in taskManager.Active)
            {
                if (t.state == TaskInstance.TaskState.New &&        // dùng t.state cho đồng nhất
                    t.Assignee != null &&
                    (!t.Definition.UseRequiredRole || t.Assignee.Role == t.Definition.RequiredRole))
                {
                    taskManager.StartTask(t);                       // gọi TaskManager.StartTask
                    started++;
                }
            }

            if (alerts && started > 0)
                alerts.Push($" Bắt đầu {started} task");
        }

        private void RenderDebugList(List<TaskInstance> list)
        {
            if (listText == null) return;
            if (list == null || list.Count == 0)
            {
                listText.text = "(Không có task. Rảnh rồi!)";
                return;
            }
            var sb = new System.Text.StringBuilder(128);
            int row = 1;
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                int percent = Mathf.RoundToInt(t.progress01 * 100f);
                sb.AppendLine($"{row++}. {t.DisplayName} — {percent}% — {t.state}");
            }
            listText.text = sb.ToString();
        }

        //Hàm kiểm tra nhanh có task nào New và đã có assignee đúng role hay chưa:
        private bool HasAnyStartableTask()
        {
            if (taskManager == null) return false;
            var list = taskManager.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                // Start được khi: state = New, có Assignee, và (không yêu cầu role hoặc assignee đúng role)
                if (t.State == Wargency.Gameplay.TaskInstance.TaskState.New &&
                    t.Assignee != null &&
                    (!t.Definition.UseRequiredRole || t.Assignee.Role == t.Definition.RequiredRole))
                    return true;
            }
            return false;
        }
    }
}
