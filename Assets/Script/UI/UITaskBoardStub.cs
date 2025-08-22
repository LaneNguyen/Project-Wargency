using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wargency.Gameplay;

namespace Wargency.UI
{
    // UI debug: liệt kê Task đang hoạt động + nút Spawn First + Start All.
    public class UITaskBoardStub : MonoBehaviour
    {
        [Header("Refs tham chiếu")]
        public TaskManager taskManager; // Kéo từ Scene vào. Nếu bỏ trống, Awake() sẽ thử FindObjectOfType.

        [Header("UI")]
        public TextMeshProUGUI listText;    // Hiển thị danh sách task
        public Button spawnFirstButton;     // Spawn availableDefinitions[0]
        public Button startAllButton;       // Start tất cả task đang chờ

        // New update đáp ứng role M3.2
        [Header("Display Options")] // NEW
        public bool showRoleRequirement = true; // NEW
        public bool showAssignee = true;        // NEW

        private readonly StringBuilder stringBuilder = new StringBuilder();

        void Awake()
        {
            // Tự tìm TaskManager nếu quên gán
            if (taskManager == null) taskManager = FindFirstObjectByType<TaskManager>();

            // Nếu thiếu UI, tự dựng nhanh panel để "cắm là chạy"
            if (listText == null || spawnFirstButton == null || startAllButton == null)
                AutoBuildStubUI();

            // Gắn sự kiện khi bấm nút
            if (spawnFirstButton != null) spawnFirstButton.onClick.AddListener(SpawnFirst);
            if (startAllButton != null) startAllButton.onClick.AddListener(StartAll);
        }

        void Update()
        {
            if (taskManager == null || listText == null) return;

            // GIẢ ĐỊNH API M1:
            // - taskManager.Active: List<TaskInstance>
            // - TaskInstance có: DisplayName (string), Progress01 (0..1 float), State (enum)
            // Nếu khác, đổi chỗ này cho khớp dự án.
            var active = taskManager.Active; // <-- Đổi nếu tên khác

            stringBuilder.Clear();
            if (active != null && active.Count > 0)
            {
                int row = 1;
                for (int i = 0; i < active.Count; i++)
                {
                    var task = active[i];

                    // chỉ hiện những task còn chạy hoặc chưa bắt đầu
                    if (task.state == TaskInstance.TaskState.Completed ||
                        task.state == TaskInstance.TaskState.Failed) continue;

                    //M1
                    var name = task.DisplayName;
                    int percent = Mathf.RoundToInt(task.progress01 * 100f);
                    var state = task.state.ToString();

                    //M3.2
                    string requiresStr = string.Empty; // NEW
                    var def = task.definition;         // NEW
                    if (showRoleRequirement && def != null && def.HasRoleRestriction(out var reqRole)) // NEW
                    {
                        requiresStr = $" <alpha=#88>(Requires: {reqRole})</alpha>"; // NEW
                    }
                    // M3.2: TaskInstance.assignee khi có
                    string assigneeStr = string.Empty; // NEW
                    if (showAssignee && task.assignee != null) // NEW
                    {
                        assigneeStr = $" — <alpha=#AA>{task.assignee.name} ({task.assignee.Role})</alpha>"; // NEW
                    }
                    
                    
                    stringBuilder.AppendLine($"{row}. {name}{requiresStr}{assigneeStr} — {percent}% — {state}"); row++;
                }
                if (row == 1) stringBuilder.AppendLine("(Không có task. Rảnh rồi!)");
            }
            else
            {
                stringBuilder.AppendLine("(Không có task. Rảnh rồi!)");
            }

            listText.text = stringBuilder.ToString();
        }

        // Spawn availableDefinitions[0]. Dùng để smoke-test flow. Chỉ là hàm test bằng cách xuất hiện 1 task đầu tiên
        public void SpawnFirst()
        {
            if (taskManager == null) return;
            var definitions = taskManager.availableDefinitions; // <-- Đổi nếu field khác tên
            if (definitions != null && definitions.Length > 0 && definitions[0] != null)
            {
                taskManager.Spawn(definitions[0]); // <-- Đổi nếu method khác tên
            }
            else
            {
                Debug.LogWarning("[UITaskBoardStub] Không tìm thấy availableDefinitions[0].");
            }
        }

        // Bắt đầu tất cả Task đang ở trạng thái chờ/chưa chạy.
        public void StartAll()
        {
            if (taskManager == null) return;
            taskManager.StartAll(); // <-- Đổi nếu method khác tên (ví dụ: StartAll())
        }

        // ------------------- Helper: dựng UI phụ -------------------
        private void AutoBuildStubUI()
        {
            // Tạo panel gọn ở góc trái dưới
            var panel = new GameObject("TaskBoardPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(transform, false);
            var panelRectTransform = (RectTransform)panel.transform;
            panelRectTransform.anchorMin = new Vector2(0f, 0f);
            panelRectTransform.anchorMax = new Vector2(0.35f, 0.35f);
            panelRectTransform.pivot = new Vector2(0f, 0f);
            panelRectTransform.anchoredPosition = new Vector2(16, 16);

            // Text danh sách
            var textGO = new GameObject("ListText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(panel.transform, false);
            var textRectTransform = (RectTransform)textGO.transform;
            textRectTransform.anchorMin = new Vector2(0.04f, 0.32f);
            textRectTransform.anchorMax = new Vector2(0.96f, 0.98f);
            textRectTransform.offsetMin = Vector2.zero; textRectTransform.offsetMax = Vector2.zero;
            listText = textGO.GetComponent<TextMeshProUGUI>();
            listText.textWrappingMode = TextWrappingModes.NoWrap;
            listText.text = "(Không có task)";

            // Spawn First button
            spawnFirstButton = CreateButton(panel.transform, "Hiện task đầu", new Vector2(0.45f, 0.18f));
            // Start All button
            startAllButton = CreateButton(panel.transform, "Start all", new Vector2(0.8f, 0.18f));
        }

        //tạo nút bấm test
        private Button CreateButton(Transform parent, string label, Vector2 anchorCenter)
        {
            var buttonObject = new GameObject(label, typeof(RectTransform), typeof(Button), typeof(Image));
            buttonObject.transform.SetParent(parent, false);
            var rectTransform = (RectTransform)buttonObject.transform;
            rectTransform.anchorMin = anchorCenter;
            rectTransform.anchorMax = anchorCenter;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(150, 40);

            var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(buttonObject.transform, false);
            var textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero; textRect.offsetMax = Vector2.zero;
            var tmp = textObject.GetComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.text = label;

            return buttonObject.GetComponent<Button>();
        }
    }
}
