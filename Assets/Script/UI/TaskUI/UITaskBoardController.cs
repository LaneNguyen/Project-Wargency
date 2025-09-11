using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Wargency.Gameplay;

namespace Wargency.UI
{
    // UITaskBoardController — Resilient gating + stable mapping
    // - Chỉ render sau khi Storyboard kết thúc (nếu bật), có failsafe tự mở khóa
    // - Hiệu ứng slide-in dùng unscaledDeltaTime (không kẹt khi timeScale=0)
    // - Rebuild layout khi đóng storyboard
    // - Giữ mapping TaskInstance ↔ UITaskPanel ổn định, panel hoàn thành/failed được giữ để chạy effect

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
        public UILiveAlertsFeed alerts;           

        [Header("Effects")]
        [SerializeField] private GameObject effectCompletedPrefab;
        [SerializeField] private GameObject effectFailedPrefab;
        [SerializeField] private float effectLingerSeconds = 1f;  // thời gian giữ panel để chơi effect (unscaled)

        [Header("UI Limit & Slide-in")]
        [Tooltip("Giới hạn số ô hiển thị ở UI (không phụ thuộc limit nội bộ của TaskManager). 0 = không giới hạn.")]
        [SerializeField] private int maxVisibleSlots = 0;

        [Tooltip("Khoảng offset X khi panel mới trượt vào từ phải.")]
        [SerializeField] private float slideInOffsetX = 220f;

        [Tooltip("Thời gian trượt vào (giây).")]
        [SerializeField] private float slideInDuration = 0.15f;

        [Tooltip("Tên child để animate (nếu rỗng sẽ animate chính RectTransform của panel).")]
        [SerializeField] private string slideContentChildName = "Content";

        // ===== Robust Storyboard Gating =====
        [Header("Storyboard Gating")]
        [Tooltip("Chỉ render taskboard sau khi storyboard kết thúc.")]
        [SerializeField] private bool waitForStoryboard = true;
        [SerializeField] private StoryboardPanel storyboard;
        [Tooltip("Failsafe: tự mở khóa nếu chờ quá lâu (giây, unscaled). 0 = tắt failsafe")]
        [SerializeField] private float unlockTimeout = 3f;

        private bool _storyDone;
        private float _storyWaitClock; // unscaled seconds

        // Stable mapping & state
        private readonly Dictionary<TaskInstance, UITaskPanel> taskToPanel = new();
        private readonly HashSet<UITaskPanel> panelsInEffect = new();
        private readonly List<UITaskPanel> pool = new();
        private readonly HashSet<UITaskPanel> _usedThisFrame = new();
        private readonly HashSet<TaskInstance> _seenThisFrame = new();
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
            if (taskManager)
            {
                taskManager.OnTaskCompleted += HandleTaskCompleted;
                taskManager.OnTaskFailed += HandleTaskFailed;   // <-- đảm bảo có handler
            }

            // --- Robust gating init ---
            // 1) Không chờ nếu tắt gating
            if (!waitForStoryboard)
            {
                _storyDone = true;
                return;
            }

            // 2) Nếu không có storyboard / không active => coi như xong
            if (storyboard == null || !storyboard.isActiveAndEnabled || !storyboard.gameObject.activeInHierarchy)
            {
                _storyDone = true;
                return;
            }

            // 3) Đăng ký OnFinished + reset đồng hồ failsafe
            _storyDone = false;
            _storyWaitClock = 0f;
            storyboard.OnFinished += OnStoryboardFinished;
        }

        private void OnDisable()
        {
            if (taskManager)
            {
                taskManager.OnTaskCompleted -= HandleTaskCompleted;
                taskManager.OnTaskFailed -= HandleTaskFailed;
            }
            if (waitForStoryboard && storyboard != null)
                storyboard.OnFinished -= OnStoryboardFinished;
        }

        private void OnStoryboardFinished()
        {
            _storyDone = true;
            StartCoroutine(Co_RebuildAfterStoryboard());
        }

        private System.Collections.IEnumerator Co_RebuildAfterStoryboard()
        {
            yield return null; // chờ CanvasScaler 1 frame
            Canvas.ForceUpdateCanvases();
            if (panelContainer) LayoutRebuilder.ForceRebuildLayoutImmediate(panelContainer);
        }

        private void Update()
        {
            // ===== Failsafe mở khóa khi chờ quá lâu =====
            if (!_storyDone && waitForStoryboard)
            {
                _storyWaitClock += Time.unscaledDeltaTime;
                if (unlockTimeout > 0f && _storyWaitClock >= unlockTimeout)
                {
                    Debug.LogWarning("[UITaskBoardController] Storyboard chưa báo xong, auto-unlock sau timeout.");
                    _storyDone = true;
                }
            }

            if (!taskManager) return;

            // Khi chưa xong storyboard: vẫn hiện placeholder để khỏi trống
            if (!_storyDone)
            {
                if (noTasksPlaceholder) noTasksPlaceholder.SetActive(true);
                if (startAllButton) startAllButton.interactable = false;
                if (listText) listText.text = "(Đang chạy hướng dẫn...)";
                return;
            }

            // ==== Build danh sách task đang active (lọc New/Running) ====
            var active = taskManager.Active; // IReadOnlyList<TaskInstance>
            _tmp.Clear();
            for (int i = 0; i < active.Count; i++)
            {
                var t = active[i];
                if (t == null) continue;
                if (t.state == TaskInstance.TaskState.Completed || t.state == TaskInstance.TaskState.Failed) continue;
                _tmp.Add(t);
            }

            int logicalCount = _tmp.Count;
            if (maxVisibleSlots > 0 && logicalCount > maxVisibleSlots) logicalCount = maxVisibleSlots;

            EnsurePool(logicalCount);
            _usedThisFrame.Clear();
            _seenThisFrame.Clear();

            // 1) Unbind mapping tasks không còn active (nếu panel không đang effect)
            var toRemove = ListPool<TaskInstance>.Get();
            foreach (var kv in taskToPanel)
            {
                var inst = kv.Key;
                var panel = kv.Value;
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

            // 2) Bind/giữ panel cho các task theo thứ tự
            for (int i = 0; i < logicalCount; i++)
            {
                var inst = _tmp[i];
                _seenThisFrame.Add(inst);

                if (!taskToPanel.TryGetValue(inst, out var panel) || panel == null)
                {
                    panel = GetFreePanel();
                    if (panel == null) continue;

                    int desiredSibling = (_pendingVacancyIndex >= 0) ? Mathf.Clamp(_pendingVacancyIndex, 0, logicalCount - 1) : i;
                    panel.transform.SetSiblingIndex(desiredSibling);

                    if (!panel.gameObject.activeSelf) panel.gameObject.SetActive(true);
                    panel.SetData(inst);
                    var dz = panel.GetComponent<UITaskDropZone>();
                    if (dz != null) dz.Bind(inst);

                    taskToPanel[inst] = panel;
                    PlaySlideInFromRight(panel); // unscaled slide-in
                }
                else
                {
                    if (!panel.gameObject.activeSelf) panel.gameObject.SetActive(true);
                    panel.SetData(inst);
                    var dz = panel.GetComponent<UITaskDropZone>();
                    if (dz != null) dz.Bind(inst);
                }

                _usedThisFrame.Add(panel);
            }

            // 3) Ẩn panel rảnh (không dùng frame này và không đang effect)
            for (int j = 0; j < pool.Count; j++)
            {
                var p = pool[j];
                if (!p) continue;
                if (_usedThisFrame.Contains(p)) continue;
                if (panelsInEffect.Contains(p)) continue;
                if (p.gameObject.activeSelf) p.gameObject.SetActive(false);
            }

            // 4) Placeholder
            bool empty = (logicalCount == 0);
            if (noTasksPlaceholder) noTasksPlaceholder.SetActive(empty);

            // 5) Start All
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
                item.gameObject.SetActive(false); // tạo tắt để không lóe
                pool.Add(item);
            }
        }

        private UITaskPanel GetFreePanel()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                var p = pool[i];
                if (!p) continue;
                if (panelsInEffect.Contains(p)) continue;
                if (_usedThisFrame.Contains(p)) continue;
                if (p.Current != null && _seenThisFrame.Contains(p.Current)) continue;
                return p;
            }
            var item = Instantiate(itemPrefab, panelContainer);
            item.gameObject.SetActive(false);
            pool.Add(item);
            return item;
        }

        // ===== Handlers =====
        private void HandleTaskCompleted(TaskInstance inst)
        {
            if (inst == null) return;

            if (!taskToPanel.TryGetValue(inst, out var panel) || !panel)
            {
                panel = FindPanelByTask(inst);
            }

            if (panel)
            {
                _pendingVacancyIndex = panel.transform.GetSiblingIndex();

                AudioManager.Instance.PlaySE(AUDIO.SE_MOMOSOUND);
                if (alerts) alerts.Push($"Hoàn thành: \"{inst.DisplayName}\"");

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

                if (alerts) alerts.Push($"Thất bại: \"{inst.DisplayName}\"");

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

            // dùng Realtime để không phụ thuộc timeScale
            float t = 0f;
            float dur = Mathf.Max(0f, effectLingerSeconds);
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            panelsInEffect.Remove(panel);
            if (panel && panel.gameObject) panel.gameObject.SetActive(false);
        }

        // ===== Slide-in helper (unscaled) =====
        private void PlaySlideInFromRight(UITaskPanel panel)
        {
            if (!panel) return;

            RectTransform animRT = null;
            if (!string.IsNullOrEmpty(slideContentChildName))
            {
                var tr = panel.transform.Find(slideContentChildName) as RectTransform;
                if (tr) animRT = tr;
            }
            if (!animRT) animRT = panel.transform as RectTransform;

            var cg = panel.GetComponentInChildren<CanvasGroup>();
            if (!cg)
            {
                cg = panel.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
            }

            panel.StopAllCoroutines();
            panel.StartCoroutine(SlideInCoroutine(animRT, cg));
        }

        private System.Collections.IEnumerator SlideInCoroutine(RectTransform rt, CanvasGroup cg)
        {
            if (!rt) yield break;

            Vector2 end = rt.anchoredPosition;
            Vector2 start = new Vector2(end.x + Mathf.Abs(slideInOffsetX), end.y);

            float t = 0f;
            float dur = Mathf.Max(0.0001f, slideInDuration);
            rt.anchoredPosition = start;
            if (cg) cg.alpha = 0f;

            while (t < dur && rt != null)
            {
                t += Time.unscaledDeltaTime; // unscaled để không kẹt khi pause/tutorial
                float u = Mathf.Clamp01(t / dur);
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

            if (maxVisibleSlots > 0 && taskManager.Active.Count >= maxVisibleSlots)
            {
                Debug.Log("[UI] Đã đạt giới hạn hiển thị UI (maxVisibleSlots). Bỏ qua spawn smoke.");
                return;
            }

            var defs = taskManager.availableDefinitions;
            if (defs != null && defs.Length > 0 && defs[0] != null)
            {
                var inst = taskManager.Spawn(defs[0]); //chưa có assignee
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
                if (t.state == TaskInstance.TaskState.New &&
                    t.Assignee != null &&
                    (!t.Definition.UseRequiredRole || t.Assignee.Role == t.Definition.RequiredRole))
                {
                    taskManager.StartTask(t);
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

        private bool HasAnyStartableTask()
        {
            if (taskManager == null) return false;
            var list = taskManager.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                if (t.State == TaskInstance.TaskState.New &&
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
        public static List<T> Get() => _pool.Count > 0 ? _pool.Pop() : new List<T>(8);
        public static void Release(List<T> list) { list.Clear(); _pool.Push(list); }
    }
}
