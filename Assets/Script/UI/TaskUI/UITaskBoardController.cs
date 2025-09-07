using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Wargency.Gameplay;

//
// UITaskBoardController — Stable mapping edition
// - Giữ mapping TaskInstance ↔ UITaskPanel trong suốt vòng đời của task (không clear mỗi frame)
// - Chỉ gán sibling lần đầu khi panel xuất hiện (hoặc khi lấp slot trống do complete/failed)
// - Panel của task đã xong sẽ được giữ lại để chơi effect, mapping được release ngay khi effect bắt đầu
// - Tránh case panel mới “đè” lên panel cũ đang chạy
//
// Lưu ý: UITaskPanel.Current cần public getter (đã có sẵn trong file của bạn)
//
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

        // Stable mapping & state
        private readonly Dictionary<TaskInstance, UITaskPanel> taskToPanel = new();
        private readonly HashSet<UITaskPanel> panelsInEffect = new();
        private readonly List<UITaskPanel> pool = new();
        private readonly HashSet<UITaskPanel> _usedThisFrame = new();
        private readonly HashSet<TaskInstance> _seenThisFrame = new();

        // --- Hooks mở rộng ---
        public System.Func<TaskInstance, bool> Filter;
        public System.Comparison<TaskInstance> Sort;
        public System.Func<TaskInstance, string> GroupKey;

        private static readonly List<TaskInstance> _tmp = new(64);

        // Vị trí trống ưu tiên cho slide-in khi có task mới xuất hiện ngay sau khi một task kết thúc
        private int _pendingVacancyIndex = -1;

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

            // Build danh sách task đang active (lọc New/Running / không lấy Completed/Failed)
            var active = taskManager.Active; // IReadOnlyList<TaskInstance>
            _tmp.Clear();
            for (int i = 0; i < active.Count; i++)
            {
                var t = active[i];
                if (t == null) continue;
                if (t.state == TaskInstance.TaskState.Completed || t.state == TaskInstance.TaskState.Failed) continue;
                if (Filter != null && !Filter(t)) continue;
                _tmp.Add(t);
            }
            if (Sort != null) _tmp.Sort(Sort);

            int logicalCount = _tmp.Count;
            if (maxVisibleSlots > 0 && logicalCount > maxVisibleSlots)
                logicalCount = maxVisibleSlots;

            // Số panel cần = số task hiển thị (không cộng panel effect — effect giữ panel cũ riêng)
            EnsurePool(logicalCount);

            // Reset marks
            _usedThisFrame.Clear();
            _seenThisFrame.Clear();

            // 1) Unbind mapping của các task không còn active (nếu panel không đang effect)
            //    (Không tắt panel ở đây nếu nó đang chạy effect; effect coroutine tự xử lý)
            var toRemove = ListPool<TaskInstance>.Get();
            foreach (var kv in taskToPanel)
            {
                var inst = kv.Key;
                var panel = kv.Value;
                // Nếu task này không còn trong active list -> release nếu panel không effect
                bool stillActive = false;
                for (int i = 0; i < _tmp.Count; i++)
                {
                    if (ReferenceEquals(_tmp[i], inst)) { stillActive = true; break; }
                }
                if (!stillActive && panel && !panelsInEffect.Contains(panel))
                {
                    if (panel.gameObject.activeSelf) panel.gameObject.SetActive(false);
                    toRemove.Add(inst);
                }
            }
            for (int i = 0; i < toRemove.Count; i++) taskToPanel.Remove(toRemove[i]);
            ListPool<TaskInstance>.Release(toRemove);

            // 2) Bind/Giữ panel cho các task theo thứ tự (không shuffle panel đã hiện trừ lần đầu)
            for (int i = 0; i < logicalCount; i++)
            {
                var inst = _tmp[i];
                _seenThisFrame.Add(inst);

                if (!taskToPanel.TryGetValue(inst, out var panel) || panel == null)
                {
                    // Tạo/bốc panel rảnh
                    panel = GetFreePanel();
                    if (panel == null) continue;

                    // Lần đầu hiện: đặt sibling theo _pendingVacancyIndex hoặc theo index i
                    int desiredSibling = (_pendingVacancyIndex >= 0) ? Mathf.Clamp(_pendingVacancyIndex, 0, logicalCount - 1) : i;
                    panel.transform.SetSiblingIndex(desiredSibling);

                    // Bật panel và gán data
                    if (!panel.gameObject.activeSelf) panel.gameObject.SetActive(true);
                    panel.SetData(inst);
                    var dz = panel.GetComponent<UITaskDropZone>();
                    if (dz != null) dz.Bind(inst);

                    // Lưu mapping
                    taskToPanel[inst] = panel;

                    // Slide-in nhẹ khi lần đầu xuất hiện
                    PlaySlideInFromRight(panel);
                }
                else
                {
                    // Panel đã có sẵn cho task này: đảm bảo đang bật và update data (nếu cần)
                    if (!panel.gameObject.activeSelf) panel.gameObject.SetActive(true);
                    panel.SetData(inst);
                    var dz = panel.GetComponent<UITaskDropZone>();
                    if (dz != null) dz.Bind(inst);
                }

                _usedThisFrame.Add(panel);
            }

            // 3) Ẩn các panel rảnh (không dùng frame này và không đang effect)
            for (int j = 0; j < pool.Count; j++)
            {
                var p = pool[j];
                if (!p) continue;
                if (_usedThisFrame.Contains(p)) continue;
                if (panelsInEffect.Contains(p)) continue; // để effect tự ẩn
                if (p.gameObject.activeSelf) p.gameObject.SetActive(false);
            }

            // 4) Placeholder
            bool empty = (logicalCount == 0);
            if (noTasksPlaceholder) noTasksPlaceholder.SetActive(empty);

            // 5) Nút Start All: sáng nếu có ít nhất một task startable
            if (startAllButton != null)
                startAllButton.interactable = HasAnyStartableTask();

            // 6) Debug
            if (listText) RenderDebugList(_tmp);
        }

        // ==== Helpers ====
        private void EnsurePool(int need)
        {
            while (pool.Count < need)
            {
                var item = Instantiate(itemPrefab, panelContainer);
                item.gameObject.SetActive(false); // tạo ở trạng thái tắt để không lóe
                pool.Add(item);
            }
        }

        private UITaskPanel GetFreePanel()
        {
            // Ưu tiên panel đang tắt hoặc không bị effect và không dùng trong frame này
            for (int i = 0; i < pool.Count; i++)
            {
                var p = pool[i];
                if (!p) continue;
                if (panelsInEffect.Contains(p)) continue;
                if (_usedThisFrame.Contains(p)) continue;

                // Nếu panel đang hiển thị 1 task mà task đó vẫn đang active ở frame này -> không dùng
                if (p.Current != null && _seenThisFrame.Contains(p.Current)) continue;

                return p;
            }

            // Không đủ -> tạo mới
            var item = Instantiate(itemPrefab, panelContainer);
            item.gameObject.SetActive(false);
            pool.Add(item);
            return item;
        }

        // ===== Handlers =====
        private void HandleTaskCompleted(TaskInstance inst)
        {
            if (inst == null) return;

            // Tìm panel theo mapping, nếu không thấy (mapping đã release) thì fallback quét pool
            if (!taskToPanel.TryGetValue(inst, out var panel) || !panel)
            {
                panel = FindPanelByTask(inst);
            }

            if (panel)
            {
                // Ghi nhận vị trí trống để task mới lấp vào
                _pendingVacancyIndex = panel.transform.GetSiblingIndex();

                AudioManager.Instance.PlaySE(AUDIO.SE_COINPICKUP);
                if (alerts) alerts.Push($"✅ Hoàn thành: \"{inst.DisplayName}\"");

                // Release mapping ngay để slot coi như trống cho lượt bind tiếp theo,
                // nhưng vẫn giữ panel hiển thị effect cho đẹp
                taskToPanel.Remove(inst);
                StartCoroutine(PlayEffectAndRelease(panel, effectCompletedPrefab));
            }
        }

        private void HandleTaskFailed(TaskInstance inst)
        {
            if (inst == null) return;

            if (!taskToPanel.TryGetValue(inst, out var panel) || !panel)
            {
                panel = FindPanelByTask(inst);
            }

            if (panel)
            {
                _pendingVacancyIndex = panel.transform.GetSiblingIndex();

                if (alerts) alerts.Push($"❌ Thất bại: \"{inst.DisplayName}\"");

                taskToPanel.Remove(inst);
                StartCoroutine(PlayEffectAndRelease(panel, effectFailedPrefab));
            }
        }

        private UITaskPanel FindPanelByTask(TaskInstance inst)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                var p = pool[i];
                if (p && ReferenceEquals(p.Current, inst)) return p;
            }
            return null;
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

    // ===== Lightweight ListPool to avoid GC in loops =====
    internal static class ListPool<T>
    {
        private static readonly Stack<List<T>> _pool = new Stack<List<T>>();

        public static List<T> Get()
        {
            return _pool.Count > 0 ? _pool.Pop() : new List<T>(8);
        }

        public static void Release(List<T> list)
        {
            list.Clear();
            _pool.Push(list);
        }
    }
}
