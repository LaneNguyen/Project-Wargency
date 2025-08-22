using UnityEngine;
using System;
using System.Collections.Generic;

namespace Wargency.Gameplay
{
    // Quản lý spawn / tick / complete các TaskInstance.
    // Khi task Completed -> cộng Budget/Score qua GameLoopController.

    public enum AssignmentFallback
    {
        AnyAgent, // Không có agent đúng role thì gán cho bất kỳ agent nào cũng dc
        Fail      // Không có agent đúng role thì báo fail
    }
    public class TaskManager : MonoBehaviour
    {
        [Header("Definitions (tạo trong editor)")]
        [Tooltip("Danh sách các TaskDefinition được chuẩn bị sẵn trong Editor. Kéo các Task Definition vào để spawn từ UI stub")]
        public TaskDefinition[] availableDefinitions;

        [Header("Ticking")]
        [Tooltip("updateInterval <= 0: cập nhật mỗi frame (60 lần/giây ở 60 FPS). > 0: cập nhật theo khoảng thời gian cố định, ví dụ = 0.5 nghĩa là cập nhật 2 lần/giây.")]
        public float updateInterval = 0f; //ví dụ updateInterval = 0.5 nghĩa là cập nhật 2 lần/giây.

        [Header("Refs GameLoopControl")]
        [Tooltip("Để kéo ref Object có script GameLoopController vào")]
        public GameLoopController gameLoopController;

        [Header("Agents")]
        [Tooltip("Danh sách agent đang hoạt động trong scene. Gọi RegisterAgent/UnregisterAgent từ Spawner/Hiring.")]
        public List<CharacterAgent> activeAgents = new List<CharacterAgent>();

        [Header("Assignment Rules")]
        public AssignmentFallback fallbackMode = AssignmentFallback.AnyAgent;

        private readonly List<TaskInstance> active = new List<TaskInstance>();//danh sách task tồn tại lúc chơi
        public IReadOnlyList<TaskInstance> Active => active;//để UI đọc, chứ ko sửa trực tiếp

        public event Action<TaskInstance> OnTaskSpawned; // tạo sự kiện có task mới
        public event Action<TaskInstance> OnTaskCompleted; //tạo sự kiện báo task hoàn thành

        private float timeBuffer;//biến cộng dồng thời gian

        //Tăng độ khó = stress cost
        private int baseStressCost = 0;

        public TaskInstance Spawn(TaskDefinition definition)
        {
            if (definition == null) return null;
            var task = new TaskInstance(definition);

            task.stressCost = Mathf.Max(0, definition.stressImpact + baseStressCost);

            active.Add(task);
            OnTaskSpawned?.Invoke(task);
            return task;
        }

        // Update M3 - 22/08: AssignTask với ưu tiên role + fallback 
        // Trả về true nếu assign thành công (có agent được chọn). instanceOut là task đã spawn và gán assignee.
        public bool AssignTask(TaskDefinition definition, out TaskInstance instanceOut)
        {
            instanceOut = null;
            if (definition == null)
            {
                Debug.LogWarning("[TaskManager] AssignTask: definition == null");
                return false;
            }

            if (activeAgents == null || activeAgents.Count == 0)
            {
                Debug.LogWarning("[TaskManager] AssignTask: activeAgents trống.");
                return false;
            }

            CharacterAgent chosen = null;

            // 1) Nếu có yêu cầu role → lọc trước
            if (definition.UseRequiredRole)
            {
                var req = definition.RequiredRole;
                for (int i = 0; i < activeAgents.Count; i++)
                {
                    var a = activeAgents[i];
                    if (a != null && a.Role == req)
                    {
                        chosen = a;
                        break;
                    }
                }

                // 2) Không có agent role phù hợp
                if (chosen == null)
                {
                    if (fallbackMode == AssignmentFallback.Fail)
                    {
                        Debug.Log($"[TaskManager] AssignTask FAIL: thiếu agent role {req} cho '{definition.DisplayName}'.");
                        return false;
                    }
                    else
                    {
                        // AnyAgent
                        for (int i = 0; i < activeAgents.Count; i++)
                        {
                            if (activeAgents[i] != null) { chosen = activeAgents[i]; break; }
                        }
                        if (chosen == null)
                        {
                            Debug.Log($"[TaskManager] AssignTask: không có agent nào để fallback cho '{definition.DisplayName}'.");
                            return false;
                        }
                        Debug.Log($"[TaskManager] Fallback ANY: giao '{definition.DisplayName}' cho {chosen.name} do thiếu role {req}.");
                    }
                }
            }
            else
            {
                // Không yêu cầu role => chọn đại agent đầu tiên còn thở
                for (int i = 0; i < activeAgents.Count; i++)
                {
                    if (activeAgents[i] != null) { chosen = activeAgents[i]; break; }
                }
                if (chosen == null)
                {
                    Debug.Log("[TaskManager] AssignTask: activeAgents đều null?");
                    return false;
                }
            }

            // 3) Spawn task + gán assignee + Start
            var task = Spawn(definition);
            if (task == null) return false;

            task.assignee = chosen;
            StartTask(task); // dùng luồng start cũ
            instanceOut = task;

            Debug.Log($"[TaskManager] Assigned '{definition.DisplayName}' → Agent: {chosen.name} (Role: {chosen.Role})");
            return true;
        }


        public void StartTask(TaskInstance task) //hàm bắt đầu task, chuyển task sang start
        {
            if (task == null) return;
            task.Start();
        }
        public void StartAll() //hàm bắt đầu toàn bộ task đang chờ
        {
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i].state == TaskInstance.TaskState.New)
                {
                    active[i].Start();
                }
            }
        }
        public void CancelTask(TaskInstance task, bool fail = true) // đổi trạng thái taks, fail = true thì là task failled, false thì làm lại dc
        {
            if (task == null) return;
            task.Cancel(fail);
        }
        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (updateInterval <= 0f)
            {
                UpdateAllTask(deltaTime);
            }
            else
            {
                timeBuffer += deltaTime;
                while (timeBuffer > updateInterval)
                {
                    UpdateAllTask(updateInterval);
                    timeBuffer -= updateInterval;
                }
            }
        }

        // Gọi Tick() cho mọi task. Ví dụ: Task "Design Logo" cần 10 giây.
        // Nếu mỗi frame delta =1 (giả sử 1 giây), thì sau 10 lần TickAll(1), task sẽ Completed, ApplyRewards sẽ cộng Budget/Score.
        private void UpdateAllTask(float deltatime)//cập nhật tất cả task
        {
            for (int i = 0; i < active.Count; i++)
            {
                var task = active[i];
                bool justDone = task.Tick(deltatime); // trừ timeLeft theo dt, cập nhật progress01
                if (justDone && task.state == TaskInstance.TaskState.Completed)//task nào xong thì duyệt hoàn thành 
                {
                    ApplyRewards(task);
                    OnTaskCompleted?.Invoke(task);
                }
            }
        }

        private void ApplyRewards(TaskInstance task)
        {
            if (gameLoopController == null || task?.definition == null) return;

            if (task.definition.budgetReward != 0)
            { gameLoopController.AddBudget(task.definition.budgetReward); }

            if (task.definition.scoreReward != 0)
            {
                gameLoopController.AddScore(task.definition.scoreReward);
            }
        }

        //Hàm xóa task đã hoàn thành
        public void RemoveCompletedorFailed()
        {
            active.RemoveAll(task =>
            task.state == TaskInstance.TaskState.Completed ||
            task.state == TaskInstance.TaskState.Failed);
        }

        //Tăng độ khó ở m3
        public void IncreaseStressCost(int stresscost)
        {
            baseStressCost += stresscost;
            if (baseStressCost < 0) baseStressCost = 0;
        }

        public void ContributeProgress(TaskInstance task, float delta01)
        {
            if (task == null || delta01 <= 0f) return;
            task.AddProgress(delta01); 
        }

        // ====== Agent registry (gọi từ Spawner/Hiring) ======
        public void RegisterAgent(CharacterAgent agent)
        {
            if (agent != null && !activeAgents.Contains(agent))
                activeAgents.Add(agent);
        }
        public void UnregisterAgent(CharacterAgent agent)
        {
            if (agent != null) activeAgents.Remove(agent);
        }
    }
}
