using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Wargency.Gameplay;

namespace Wargency.UI
{
    // QUẢN LÝ danh sách panel nhỏ: spawn/pool và SetData cho từng UITaskItemPanel
    public class UITaskBoardController : MonoBehaviour
    {
        [Header("Refs")]
        public TaskManager taskManager;             // kéo từ scene (nếu trống sẽ tự Find)
        public RectTransform panelContainer;        // nơi chứa các item
        public UITaskPanel itemPrefab;          // prefab panel nhỏ (có script UITaskItemPanel)

        [Header("Optional: bảng text & nút test (giữ từ stub cũ)")]
        public TextMeshProUGUI listText;            // nếu muốn hiển thị text debug song song
        public Button spawnFirstButton;
        public Button startAllButton;


        [Header("Effects")]
        [SerializeField] private GameObject effectCompletedPrefab;   // Prefab hiệu ứng hoàn thành task
        [SerializeField] private GameObject effectFailedPrefab;      // Prefab hiệu ứng fail màn
        [SerializeField, Tooltip("Giữ panel lại sau khi phát effect (giây)")]
        private float effectLingerSeconds = 1f;

        // Tìm panel theo TaskInstance
        private readonly Dictionary<TaskInstance, UITaskPanel> taskToPanel = new();
        // Các panel đang phát effect (ko “tái sử dụng/ẩn” trong lúc này)
        private readonly HashSet<UITaskPanel> panelsInEffect = new();

        // pool để tái sử dụng
        private readonly List<UITaskPanel> pool = new();


        private void Awake()
        {
            if (taskManager == null) taskManager = FindFirstObjectByType<TaskManager>();

            if (spawnFirstButton != null) spawnFirstButton.onClick.AddListener(SpawnFirst);
            if (startAllButton != null) startAllButton.onClick.AddListener(StartAll);

            // cảnh báo quên kéo tham chiếu
            if (panelContainer == null)
                Debug.LogWarning("[UITaskBoardController] panelContainer chưa được gán.");
            if (itemPrefab == null)
                Debug.LogWarning("[UITaskBoardController] itemPrefab chưa được gán (cần prefab có UITaskItemPanel).");
        }

        private void OnEnable()
        {
            if (taskManager == null) return;
            taskManager.OnTaskCompleted += HandleTaskCompleted;
            taskManager.OnTaskFailed += HandleTaskFailed;
        }

        private void Update()
        {
            if (taskManager == null) return;

            // 1) Lấy danh sách task đang hoạt động (New/InProgress)
            var active = taskManager.Active; // IReadOnlyList<TaskInstance>

            // 2) Tính số panel cần hiển thị = số task active + số panel đang phát effect
            int requiredCount = 0;
            for (int i = 0; i < active.Count; i++)
            {
                var st = active[i].state;
                if (st != TaskInstance.TaskState.Completed && st != TaskInstance.TaskState.Failed)
                    requiredCount++;
            }
            requiredCount += panelsInEffect.Count; // NEW: giữ chỗ cho panel đang chạy effect

            // 3) Bảo đảm pool đủ số lượng
            EnsurePool(requiredCount);

            // 4) Bind dữ liệu task → panel (bỏ qua panel đang chạy effect)
            int vi = 0;
            taskToPanel.Clear(); //remap mỗi frame

            for (int i = 0; i < active.Count; i++)
            {
                var inst = active[i];
                if (inst.state == TaskInstance.TaskState.Completed || inst.state == TaskInstance.TaskState.Failed)
                    continue; // không bind task đã xong/failed (panel của chúng sẽ do effect coroutine giữ)

                // Lấy panel tiếp theo trong pool
                var item = pool[vi++];

                // NEW: bỏ qua nếu panel này đang phát effect (để không tái sử dụng ngay)
                if (panelsInEffect.Contains(item))
                {
                    vi--;               // trả lại slot này để dùng cho inst bằng một panel khác
                                        // tìm panel kế tiếp không bận effect
                    bool found = false;
                    for (int seek = vi; seek < pool.Count; seek++)
                    {
                        if (!panelsInEffect.Contains(pool[seek]))
                        {
                            item = pool[seek];
                            // swap vị trí để giữ tính đơn giản
                            pool[seek] = pool[vi];
                            pool[vi] = item;
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        // Không còn panel rảnh (hiếm khi xảy ra do requiredCount đã cộng panelsInEffect)
                        // Bỏ qua bind frame này
                        continue;
                    }
                    vi++; // đã dùng được item mới
                }

                if (!item.gameObject.activeSelf) item.gameObject.SetActive(true);
                item.SetData(inst);

                // cập nhật map task => panel cho các handler event
                taskToPanel[inst] = item;
            }

            // 5) Ẩn các panel thừa (không đụng panel đang chạy effect)
            for (int j = vi; j < pool.Count; j++)
            {
                var p = pool[j];
                if (!panelsInEffect.Contains(p) && p.gameObject.activeSelf)
                    p.gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            if (taskManager == null) return;
            taskManager.OnTaskCompleted -= HandleTaskCompleted;
            taskManager.OnTaskFailed -= HandleTaskFailed;
        }
        private void EnsurePool(int need)
        {
            // tạo thêm nếu thiếu
            while (pool.Count < need)
            {
                var item = Instantiate(itemPrefab, panelContainer);
                pool.Add(item);

            }
        }

        //M3.4
        private void HandleTaskCompleted(TaskInstance inst)
        {
            if (inst == null) return;
            if (!taskToPanel.TryGetValue(inst, out var panel) || panel == null) return;
            StartCoroutine(PlayEffectAndRelease(panel, effectCompletedPrefab));
        }

        private void HandleTaskFailed(TaskInstance inst)
        {
            if (inst == null) return;
            if (!taskToPanel.TryGetValue(inst, out var panel) || panel == null) return;
            StartCoroutine(PlayEffectAndRelease(panel, effectFailedPrefab));
        }

        private System.Collections.IEnumerator PlayEffectAndRelease(UITaskPanel panel, GameObject effectPrefab)
        {
            if (panel == null) yield break;

            // Đánh dấu panel đang chạy effect để Update không tái sử dụng
            panelsInEffect.Add(panel);

            // Gọi panel tự spawn effect (prefab do bạn gắn ở Inspector)
            panel.PlayEffect(effectPrefab);

            // Giữ vài giây để người chơi thấy
            yield return new WaitForSeconds(effectLingerSeconds);

            // Hết effect → panel rảnh
            panelsInEffect.Remove(panel);

            // Ẩn panel cho pool tái sử dụng
            if (panel != null && panel.gameObject != null)
                panel.gameObject.SetActive(false);
        }

        // ——— Optional: giữ lại 2 nút smoke test giống stub cũ ———
        public void SpawnFirst()
        {
            if (taskManager == null) return;
            var defs = taskManager.availableDefinitions;
            if (defs != null && defs.Length > 0 && defs[0] != null)
            {
                // ĐỔI từ Spawn(...) -> AssignTask(...)
                if (!taskManager.AssignTask(defs[0], out var inst))
                {
                    Debug.LogWarning("[UI] AssignTask thất bại (thiếu agent đúng role và fallback = Fail?)");
                }
            }
            else
            {
                Debug.LogWarning("[UI] Không tìm thấy availableDefinitions[0]");
            }
        }
        private void StartAll()
        {
            if (taskManager == null) return;
            taskManager.StartAll();
        }

        private void RenderDebugList(IReadOnlyList<TaskInstance> active)
        {
            if (active == null || active.Count == 0) { listText.text = "(Không có task. Rảnh rồi!)"; return; }

            var sb = new System.Text.StringBuilder();
            int row = 1;
            for (int i = 0; i < active.Count; i++)
            {
                var t = active[i];
                if (t.state == TaskInstance.TaskState.Completed || t.state == TaskInstance.TaskState.Failed) continue;
                int percent = Mathf.RoundToInt(t.progress01 * 100f);
                sb.AppendLine($"{row++}. {t.DisplayName} — {percent}% — {t.state}");
            }
            if (row == 1) sb.AppendLine("(Không có task. Rảnh rồi!)");
            listText.text = sb.ToString();
        }
    }
}
