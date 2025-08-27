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
        [Tooltip("Danh sách agent đang hoạt động trong scene. Gọi RegisterAgent/UnregisterAgent từ Spawner/Hiring")]
        public List<CharacterAgent> activeAgents = new List<CharacterAgent>();

        [Header("Assignment Rules")]
        public AssignmentFallback fallbackMode = AssignmentFallback.AnyAgent;

        //UPDATE 24.08
        [Header("Limits")]                              
        [Tooltip("Giới hạn số task đang còn hiệu lực (New/InProgress).")] 
        public int maxConcurrentTasks = 3;     
        
        private readonly List<TaskInstance> active = new List<TaskInstance>();//danh sách task tồn tại lúc chơi
        public IReadOnlyList<TaskInstance> Active => active;//để UI đọc, chứ ko sửa trực tiếp

        // === NEW getters cho M3.3 ===
        // UI đọc danh sách instance (read-only)
        public IReadOnlyList<TaskInstance> ActiveInstances => active;
        //Số task đang hiệu lực (New/InProgress)
        public int ActiveCount => CountActiveEffectiveTasks();
        // Expose limit để spawner tham chiếu
        public int MaxConcurrentTasks => maxConcurrentTasks;
        // Expose danh sách agent đang hoạt động (spawner/DnD dùng
        public List<CharacterAgent> ActiveAgents => activeAgents;


        public event Action<TaskInstance> OnTaskSpawned; // tạo sự kiện có task mới
        public event Action<TaskInstance> OnTaskCompleted; //tạo sự kiện báo task hoàn thành
        public event Action<TaskInstance> OnTaskFailed;//sự kiện báo khi task failed

        private float timeBuffer;//biến cộng dồng thời gian


        //Tăng độ khó = stress cost
        private int baseStressCost = 0;

        public TaskInstance Spawn(TaskDefinition definition)
        {
            if (definition == null) return null;

            // Kiểm tra giới hạn task trước khi tạo task
            if (CountActiveEffectiveTasks() >= maxConcurrentTasks)
            {
                Debug.LogWarning($"[TaskManager] Đạt giới hạn {maxConcurrentTasks} task đang hoạt động. Không spawn thêm nhé");
                return null;
            }

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

            // Chặn assign khi đã đủ task
            if (CountActiveEffectiveTasks() >= maxConcurrentTasks)
            {
                Debug.LogWarning($"[TaskManager] AssignTask bị chặn: đã đạt max {maxConcurrentTasks} task hoạt động");
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


        public void StartTask(TaskInstance task)
        {
            if (task == null) return;

            // Update 24/08 -CHẶN: nếu task yêu cầu role mà chưa có assignee đúng
            if (!MeetsRoleRequirement(task.definition, task.assignee))
            {
                Debug.LogWarning($"[TaskManager] Không thể Start '{task?.DisplayName}' vì thiếu assignee role {task?.definition?.RequiredRole}.");
                return; // quan trọng: ĐỪNG cho chạy
            }

            task.Start();
        }

        public void StartAll()
        {
            for (int i = 0; i < active.Count; i++)
            {
                var t = active[i];
                if (t.state == TaskInstance.TaskState.New)
                {
                    if (MeetsRoleRequirement(t.definition, t.assignee))
                        t.Start();
                    else
                        Debug.LogWarning($"[TaskManager] Bỏ qua Start '{t.DisplayName}' (thiếu assignee role {t.definition.RequiredRole}).");
                }
            }
        }
        public void CancelTask(TaskInstance task, bool fail = true) // đổi trạng thái taks, fail = true thì là task failled, false thì làm lại dc
        {
            if (task == null) return;
            task.Cancel(fail);

            if (fail)
            {
                OnTaskFailed?.Invoke(task);
            }
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

        // M3.3 update: Reassign task sang agent mới (giữ progress). Tự validate role 
        public bool Reassign(TaskInstance task, CharacterAgent newAssignee)
        {
            if (task == null || newAssignee == null) return false;

            var def = task.Definition;
            if (def != null && def.UseRequiredRole && newAssignee.Role != def.RequiredRole)
            {
                Debug.LogWarning("[TaskManager] Reassign failed: role mismatch.");
                return false;
            }

            var old = task.assignee;
            if (old != null)
            {
                // Giải phóng an toàn phía agent cũ 
                old.OnExternalTaskTerminated(task);
            }

            task.assignee = newAssignee;

            // Nếu task còn New → start
            if (task.state == TaskInstance.TaskState.New)
            {
                StartTask(task);
            }
            // Nếu đang InProgress → thông báo agent mới tiếp tục
            else if (task.state == TaskInstance.TaskState.InProgess)
            {
                newAssignee.AssignTask(task);
            }

            Debug.Log($"[TaskManager] Reassigned '{task.DisplayName}' → {newAssignee.DisplayName} ({newAssignee.Role})");
            return true;
        }


        //============ Helper =====


        private bool MeetsRoleRequirement(TaskDefinition def, CharacterAgent assignee)
        {
            if (def == null) return false;
            if (!def.UseRequiredRole) return true;                  // task không yêu cầu role
            return assignee != null && assignee.Role == def.RequiredRole; // phải có assignee đúng role
        }
        // ====== Agent registry (gọi từ Spawner/Hiring) ======
        public void RegisterAgent(CharacterAgent agent)
        {
            if (agent != null && !activeAgents.Contains(agent))
            {
                activeAgents.Add(agent);
                Debug.Log($"[TaskManager] RegisterAgent: {agent.name} (Role: {agent.Role})");//debug log 24:08
            }
        }
        public void UnregisterAgent(CharacterAgent agent)
        {
            if (agent != null) activeAgents.Remove(agent);
        }

        private int CountActiveEffectiveTasks()
        {
            int c = 0;
            for (int i = 0; i < active.Count; i++)
            {
                var st = active[i].state;
                if (st != TaskInstance.TaskState.Completed && st != TaskInstance.TaskState.Failed)
                    c++;
            }
            return c;
        }

        // Spawner dùng để kiểm tra có ít nhất 1 agent đúng role không.
        public bool HasActiveAgentWithRole(CharacterRole role)
        {
            if (activeAgents == null || activeAgents.Count == 0) return false;
            for (int i = 0; i < activeAgents.Count; i++)
            {
                var a = activeAgents[i];
                if (a != null && a.Role == role) return true;
            }
            return false;
        }
    }
}
