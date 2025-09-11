using UnityEngine;
using System;
using System.Collections.Generic;
using Wargency.UI;
using Wargency.Systems;

namespace Wargency.Gameplay
{
    public enum AssignmentFallback
    {
        AnyAgent,
        Fail
    }

    public partial class TaskManager : BaseManager<TaskManager>, IResettable
    {
        [Header("Definitions (tạo trong editor)")]
        public TaskDefinition[] availableDefinitions;

        [Header("Ticking")]
        public float updateInterval = 0f;

        [Header("Refs GameLoopControl")]
        public GameLoopController gameLoopController;

        [Header("Agents")]
        public List<CharacterAgent> activeAgents = new List<CharacterAgent>();

        [Header("Assignment Rules")]
        public AssignmentFallback fallbackMode = AssignmentFallback.AnyAgent;

        [Header("Limits")]
        public int maxConcurrentTasks = 3;


        [Header("Auto Assign")]
        [SerializeField] public bool autoAssignUnlocked = false;
        [SerializeField] public bool autoAssignEnabled = false;
        [SerializeField] public bool autoStartOnAssign = true;
        [SerializeField] private Transform runtimeTaskRoot; // Kéo parent chứa toàn bộ task spawn


        private readonly List<TaskInstance> active = new List<TaskInstance>();
        public IReadOnlyList<TaskInstance> Active => active;

        public IReadOnlyList<TaskInstance> ActiveInstances => active;
        public int ActiveCount => CountActiveEffectiveTasks();
        public int MaxConcurrentTasks => maxConcurrentTasks;
        public List<CharacterAgent> ActiveAgents => activeAgents;

        public event Action<TaskInstance> OnTaskSpawned;
        public event Action<TaskInstance> OnTaskCompleted;
        public event Action<TaskInstance> OnTaskFailed;
        public event Action<TaskInstance> OnTaskStarted;

        private float timeBuffer;
        private int baseStressCost = 0;

        private readonly Dictionary<TaskInstance, CharacterAgent> rememberedAssignee = new Dictionary<TaskInstance, CharacterAgent>();
        private readonly HashSet<TaskInstance> completionProcessed = new HashSet<TaskInstance>();

        //Update 0905
        [Header("Spawn Rules")]
        [SerializeField] private bool blockSpawnWithoutRequiredRole = true;

        public TaskInstance Spawn(TaskDefinition definition)
        {
            if (definition == null) return null;

            int currentWave = gameLoopController ? gameLoopController.Wave : 1;
            if (!definition.IsAvailableAtWave(currentWave))
            {
                Debug.Log($"[TaskManager] Skip spawn '{definition.DisplayName}' => không thuộc wave {currentWave}.");
                return null;
            }

            // Update 0910: chặn spawn nếu task đòi role mà đội chưa có agent role đó
            if (blockSpawnWithoutRequiredRole
                && definition.UseRequiredRole
                && !HasActiveAgentWithRole(definition.RequiredRole))
            {
                Debug.Log($"[TaskManager] Skip spawn '{definition.DisplayName}' => thiếu agent role {definition.RequiredRole}.");
                return null;
            }
            // <<<

            if (CountActiveEffectiveTasks() >= maxConcurrentTasks)
            {
                Debug.LogWarning($"[TaskManager] Đạt giới hạn {maxConcurrentTasks} task đang hoạt động. Không spawn thêm");
                return null;
            }

            var task = new TaskInstance(definition);
            task.stressCost = Mathf.Max(0, definition.stressImpact + baseStressCost);

            active.Add(task);
            OnTaskSpawned?.Invoke(task);
            return task;
        }

        public bool AssignTask(TaskDefinition definition, out TaskInstance instanceOut)
        {
            instanceOut = null;
            if (definition == null)
            {
                Debug.LogWarning("[TaskManager] AssignTask: definition == null");
                return false;
            }

            int currentWave = gameLoopController ? gameLoopController.Wave : 1;
            if (!definition.IsAvailableAtWave(currentWave))
            {
                Debug.Log($"[TaskManager] AssignTask skip '{definition.DisplayName}' => không thuộc wave {currentWave}.");
                return false;
            }

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

            if (definition.UseRequiredRole)
            {
                var req = definition.RequiredRole;
                for (int i = 0; i < activeAgents.Count; i++)
                {
                    var a = activeAgents[i];
                    if (a != null && a.Role == req) { chosen = a; break; }
                }

                if (chosen == null)
                {
                    if (fallbackMode == AssignmentFallback.Fail)
                    {
                        Debug.Log($"[TaskManager] AssignTask FAIL: thiếu agent role {req} cho '{definition.DisplayName}'.");
                        return false;
                    }
                    else
                    {
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

            var task = Spawn(definition);
            if (task == null) return false;

            var assignRes = task.AssignCharacter(chosen);
            if (assignRes != AssignResult.Success)
            {
                Debug.LogWarning($"[TaskManager] AssignCharacter failed: {assignRes} for '{definition.DisplayName}'.");
            }

            StartTask(task);
            instanceOut = task;

            Debug.Log($"[TaskManager] Assigned '{definition.DisplayName}' => Agent: {chosen.name} (Role: {chosen.Role})");
            AudioManager.Instance.PlaySE("FlopEffect");
            return true;
        }

        public void StartTask(TaskInstance task)
        {
            if (task == null) return;

            if (task.Assignee == null)
            {
                Debug.LogWarning($"[TaskManager] Cannot Start '{task?.DisplayName}': chưa gán ai cả");
                return;
            }

            if (!MeetsRoleRequirement(task.definition, task.Assignee))
            {
                Debug.LogWarning($"[TaskManager] Không thể Start '{task?.DisplayName}' vì thiếu assignee role {task?.definition?.RequiredRole}.");
                return;
            }

            rememberedAssignee[task] = task.Assignee;

            task.Start();
            OnTaskStarted?.Invoke(task);
        }

        public void StartAll()
        {
            for (int i = 0; i < active.Count; i++)
            {
                var t = active[i];
                if (t.state == TaskInstance.TaskState.New)
                {
                    if (MeetsRoleRequirement(t.definition, t.Assignee))
                        StartTask(t);
                    else
                        Debug.LogWarning($"[TaskManager] Bỏ qua Start '{t.DisplayName}' (thiếu assignee role {t.definition.RequiredRole}).");
                }
            }
        }

        public void CancelTask(TaskInstance task, bool fail = true)
        {
            if (task == null) return;
            task.Cancel(fail);

            if (fail)
                OnTaskFailed?.Invoke(task);
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

        private void UpdateAllTask(float deltatime)
        {
            for (int i = 0; i < active.Count; i++)
            {
                var task = active[i];
                bool justDone = task.Tick(deltatime);

                if (task.state == TaskInstance.TaskState.Completed && !completionProcessed.Contains(task))
                {
                    OnTaskCompleted?.Invoke(task);
                    completionProcessed.Add(task);
                }
            }
        }

        private void ApplyRewards(TaskInstance task) { /* giữ lại stub */ }

        private void ApplyCompletionEffects(TaskInstance task)
        {
            if (task == null) return;

            var assignee = task.Assignee;
            if (assignee == null && !rememberedAssignee.TryGetValue(task, out assignee))
            {
                Debug.LogWarning($"[TaskManager] ApplyCompletionEffects: thiếu assignee cho '{task.DisplayName}', bỏ qua.");
                return;
            }

            var stats = assignee.GetComponent<CharacterStats>();
            if (stats == null) return;

            var def = task.Definition;
            if (def != null)
            {
                int energyDeltaOnComplete = -Mathf.Abs(def.energyCost);
                int stressDeltaOnComplete = +Mathf.Abs(def.stressImpact);
                stats.ApplyDelta(energyDeltaOnComplete, stressDeltaOnComplete);
                stats.Notify();
            }

            assignee.ReleaseTask();
        }

        public void RemoveCompletedorFailed()
        {
            active.RemoveAll(task =>
                task.state == TaskInstance.TaskState.Completed ||
                task.state == TaskInstance.TaskState.Failed);
        }

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

        public bool Reassign(TaskInstance task, CharacterAgent newAssignee)
        {
            if (task == null || newAssignee == null) return false;

            var def = task.Definition;
            if (def != null && def.UseRequiredRole && newAssignee.Role != def.RequiredRole)
            {
                Debug.LogWarning("[TaskManager] Reassign failed: role mismatch.");
                return false;
            }

            var old = task.Assignee;
            if (old != null)
            {
                old.OnExternalTaskTerminated(task);
            }

            try
            {
                var pi = typeof(TaskInstance).GetProperty("Assignee", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                var setm = pi?.GetSetMethod(true);
                if (setm != null) setm.Invoke(task, new object[] { newAssignee });
                else Debug.LogWarning("[TaskManager] Unable to set Assignee via reflection.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TaskManager] Reflection set Assignee failed: {ex.Message}");
                return false;
            }

            if (task.state == TaskInstance.TaskState.InProgess)
            {
                newAssignee.AssignTask(task);
            }

            rememberedAssignee[task] = newAssignee;

            Debug.Log($"[TaskManager] Reassigned '{task.DisplayName}' => {newAssignee.DisplayName} ({newAssignee.Role})");
            return true;
        }

        private bool MeetsRoleRequirement(TaskDefinition def, CharacterAgent assignee)
        {
            if (def == null) return false;
            if (!def.UseRequiredRole) return true;
            return assignee != null && assignee.Role == def.RequiredRole;
        }

        public void RegisterAgent(CharacterAgent agent)
        {
            if (agent != null && !activeAgents.Contains(agent))
            {
                activeAgents.Add(agent);
                Debug.Log($"[TaskManager] RegisterAgent: {agent.name} (Role: {agent.Role})");
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

        public void ResetState()
        {
            StopAllCoroutines();

            if (runtimeTaskRoot != null)
            {
                for (int i = runtimeTaskRoot.childCount - 1; i >= 0; i--)
                {
                    var child = runtimeTaskRoot.GetChild(i);
                    if (child != null) Destroy(child.gameObject);
                }
            }
            else
            {
                // fallback: xóa mọi child dưới chính TaskManager 
                for (int i = transform.childCount - 1; i >= 0; i--)
                {
                    var child = transform.GetChild(i);
                    if (child != null) Destroy(child.gameObject);
                }
            }
        }
        private void OnEnable()
        {
            OnTaskSpawned += HandleAutoAssignOnSpawn;
        }

        private void OnDisable()
        {
            OnTaskSpawned -= HandleAutoAssignOnSpawn;
        }

        public void UnlockAutoAssignFeature()
        {
            autoAssignUnlocked = true;
        }

        public void SetAutoAssignEnabled(bool enabled)
        {
            if (enabled && !autoAssignUnlocked) return;
            autoAssignEnabled = enabled;
            if (autoAssignEnabled) AutoAssignExistingOpenTasks(true);
        }

        public int AutoAssignExistingOpenTasks(bool onlyUnassigned = true)
        {
            int count = 0;
            for (int i = 0; i < active.Count; i++)
            {
                var tsk = active[i];
                if (tsk == null) continue;
                if (tsk.state != TaskInstance.TaskState.New) continue;
                if (onlyUnassigned && tsk.Assignee != null) continue;
                if (AutoAssign(tsk)) count++;
            }
            return count;
        }

        private void HandleAutoAssignOnSpawn(TaskInstance task)
        {
            if (!autoAssignEnabled || task == null) return;
            AutoAssign(task);
        }

        public bool AutoAssign(TaskInstance task)
        {
            if (task == null) return false;
            var def = task.Definition;
            if (def == null) return false;

            // pick agent similar to AssignTask
            CharacterAgent chosen = null;

            if (def.UseRequiredRole)
            {
                var req = def.RequiredRole;
                for (int i = 0; i < activeAgents.Count; i++)
                {
                    var a = activeAgents[i];
                    if (a != null && a.Role == req) { chosen = a; break; }
                }

                if (chosen == null)
                {
                    if (fallbackMode == AssignmentFallback.Fail) return false;
                    for (int i = 0; i < activeAgents.Count; i++)
                    {
                        if (activeAgents[i] != null) { chosen = activeAgents[i]; break; }
                    }
                    if (chosen == null) return false;
                }
            }
            else
            {
                for (int i = 0; i < activeAgents.Count; i++)
                {
                    if (activeAgents[i] != null) { chosen = activeAgents[i]; break; }
                }
                if (chosen == null) return false;
            }

            var res = task.AssignCharacter(chosen);
            if (res != AssignResult.Success) return false;

            if (autoStartOnAssign) StartTask(task);
            return true;
        }

    }
}
