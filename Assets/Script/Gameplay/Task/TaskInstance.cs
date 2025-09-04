using System;
using UnityEngine;

namespace Wargency.Gameplay
{
    public enum AssignResult { Success, RoleMismatch, AlreadyAssigned, Invalid }

    // 1 task cụ thể khi đã spawn trong game
    [Serializable]
    public class TaskInstance
    {
        public enum TaskState { New, InProgess, Completed, Failed }

        public TaskDefinition definition;

        [Range(0, 1)] public float progress01;
        public float timeLeft;
        public TaskState state { get; private set; }

        // ai đang làm task này (có thể null)
        public CharacterAgent Assignee { get; private set; }

        // Manager sẽ set = def.stressImpact + baseStressCost
        public int stressCost = -1;

        public string DisplayName => definition != null ? definition.displayName : "(Task không tên)";
        public float Progress01 => progress01;
        public TaskState State => state;
        public TaskDefinition Definition => definition;

        private bool _completionApplied = false; // đảm bảo chỉ cộng thưởng/ảnh hưởng 1 lần

        public TaskInstance(TaskDefinition definition)
        {
            this.definition = definition;
            progress01 = 0f;
            timeLeft = Mathf.Max(0.01f, definition.durationSecond);
            state = TaskState.New;
        }

        public TaskInstance(TaskDefinition definition, CharacterAgent assignee) : this(definition)
        {
            this.Assignee = assignee;
        }

        // chạy tick, trả true nếu vừa Completed
        public bool Tick(float deltaTime)
        {
            if (state != TaskState.InProgess) return false;

            timeLeft -= Mathf.Max(0f, deltaTime);
            float duration = Mathf.Max(0.001f, definition.durationSecond);
            progress01 = Mathf.Clamp01(1f - (timeLeft / duration));

            if (timeLeft <= 0f)
            {
                timeLeft = 0f;
                progress01 = 1f;
                Complete();
                return true;
            }
            return false;
        }

        public void Start()
        {
            if (state == TaskState.New)
                state = TaskState.InProgess;
        }

        public void Cancel(bool fail = false)
        {
            state = fail ? TaskState.Failed : TaskState.New;
        }

        public void ForceCompleteFromManager()
        {
            Complete();
        }

        private void Complete()
        {
            if (_completionApplied && state == TaskState.Completed) return;

            int money = 0, score = 0;
            if (Definition != null)
            {
                // lấy reward từ def
                var piMoney = Definition.GetType().GetField("RewardMoney");
                var piScore = Definition.GetType().GetField("RewardScore");
                if (piMoney != null) money = (int)piMoney.GetValue(Definition);
                if (piScore != null) score = (int)piScore.GetValue(Definition);

                // fallback cũ
                if (money == 0)
                {
                    var f = Definition.GetType().GetField("budgetReward");
                    if (f != null) money = (int)f.GetValue(Definition);
                }
                if (score == 0)
                {
                    var f = Definition.GetType().GetField("scoreReward");
                    if (f != null) score = (int)f.GetValue(Definition);
                }

                if (BudgetController.I != null) BudgetController.I.Add(money);
                else Debug.LogError("[TaskInstance] BudgetController.I null!");

                if (AgencyScoreController.I != null) AgencyScoreController.I.AddScore(score);
                else Debug.LogError("[TaskInstance] AgencyScoreController.I null!");
            }
            else
            {
                Debug.LogError("[TaskInstance] Definition null!");
            }

            TryApplyCompletionEffectsOnce();
            state = TaskState.Completed;
        }

        // Energy giảm, Stress tăng
        private void TryApplyCompletionEffectsOnce()
        {
            if (_completionApplied) return;

            var agent = Assignee;
            if (agent != null)
            {
                var stats = agent.GetComponent<CharacterStats>();
                if (stats != null)
                {
                    int energyDelta = (definition != null) ? -Mathf.Abs(definition.energyCost) : 0;
                    int stressSrc = (stressCost >= 0) ? stressCost : (definition != null ? definition.stressImpact : 0);
                    int stressDelta = +Mathf.Abs(stressSrc);

                    Debug.Log($"[TaskInstance] Complete '{DisplayName}': Energy {energyDelta}, Stress +{Mathf.Abs(stressSrc)} on {agent?.name}");

                    stats.ApplyDelta(energyDelta, stressDelta);
                    stats.Notify();
                }
                agent.ReleaseTask();
            }

            _completionApplied = true;
        }

        public void AddProgress(float delta01)
        {
            if (delta01 <= 0f || state == TaskState.Completed) return;

            progress01 = Mathf.Clamp01(progress01 + delta01);
            if (progress01 >= 1f) Complete();
        }

        public AssignResult AssignCharacter(CharacterAgent agent)
        {
            if (agent == null) return AssignResult.Invalid;

            // nếu đã đúng rồi => ok
            if (Assignee == agent && agent.CurrentAssignment == this)
                return AssignResult.Success;

            // nếu agent đang làm task khác
            if (agent.CurrentAssignment != null && agent.CurrentAssignment != this)
            {
                var other = agent.CurrentAssignment;
                if (other.State == TaskState.InProgess)
                    return AssignResult.AlreadyAssigned;

                other.Assignee = null;
                other.__ClearAssignmentSilent();
            }

            // check role
            bool roleOk = false;
            if (definition != null)
            {
                try
                {
                    var allowed = definition.GetType().GetField("AllowedRoles");
                    if (allowed != null)
                    {
                        var list = allowed.GetValue(definition) as System.Collections.IEnumerable;
                        if (list != null)
                        {
                            foreach (var r in list)
                                if (r != null && r.Equals(agent.Role)) { roleOk = true; break; }
                        }
                    }
                    else
                    {
                        var req = definition.GetType().GetField("RequiredRole");
                        if (req != null)
                        {
                            var reqVal = req.GetValue(definition);
                            if (reqVal != null && reqVal.Equals(agent.Role)) roleOk = true;
                        }
                        else roleOk = true;
                    }
                }
                catch { roleOk = true; }
            }
            else roleOk = true;

            if (!roleOk) return AssignResult.RoleMismatch;

            if (Assignee != null && Assignee != agent)
                Assignee.__SetAssignment(null);

            Assignee = agent;
            agent.__SetAssignment(this);

            return AssignResult.Success;
        }

        public bool CanStart(out string reason)
        {
            if (state != TaskState.New) { reason = "Not in New state"; return false; }
            if (Assignee == null) { reason = "No assignee"; return false; }

            if (definition != null && definition.GetType().GetField("UseRequiredRole") is var f && f != null)
            {
                bool use = (bool)f.GetValue(definition);
                if (use)
                {
                    var reqF = definition.GetType().GetField("RequiredRole");
                    if (reqF != null)
                    {
                        var reqVal = reqF.GetValue(definition);
                        if (!reqVal.Equals(Assignee.Role)) { reason = "Role mismatch"; return false; }
                    }
                }
            }

            reason = null;
            return true;
        }

        internal void __ClearAssignmentSilent()
        {
            if (Assignee != null) Assignee.__SetAssignment(null);
            Assignee = null;
        }
    }
}
