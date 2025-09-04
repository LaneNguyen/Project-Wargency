using System.Collections.Generic;
using UnityEngine;

namespace Wargency.Gameplay
{
    [DisallowMultipleComponent]
    public class LeaderSkillManager : MonoBehaviour
    {
        [Header("Skill Pool")]
        [SerializeField] private List<LeaderSkillDefinition> skillPool = new();

        [Header("Popup")]
        [SerializeField] private UIPopupManager popupManager;
        [SerializeField] private Color popupColorPositive = new Color(0.2f, 0.8f, 0.4f, 1f);
        [SerializeField] private Color popupColorNegative = new Color(0.9f, 0.2f, 0.2f, 1f);

        // // lưu cooldown theo từng SO => runtime thôi, khỏi dính Editor
        private readonly Dictionary<LeaderSkillDefinition, float> _nextReady = new();

        private void Reset()
        {
            popupManager = FindFirstObjectByType<UIPopupManager>();
        }

        // // gom agents nhanh => cho skill all-team
        private static List<CharacterAgent> CollectAgents()
        {
            var list = new List<CharacterAgent>();
            list.AddRange(FindObjectsOfType<CharacterAgent>());
            return list;
        }

        public bool IsReady(LeaderSkillDefinition skill, out float remaining)
        {
            float now = Time.unscaledTime;
            if (!_nextReady.TryGetValue(skill, out float t))
            {
                remaining = 0f;
                return true;
            }
            remaining = Mathf.Max(0f, t - now);
            return remaining <= 0f;
        }

        public float GetRemaining(LeaderSkillDefinition skill)
        {
            IsReady(skill, out float r); return r;
        }

        public bool CanAfford(LeaderSkillDefinition skill)
        {
            if (skill == null) return false;
            if (skill.deltaBudget >= 0) return true; // // thưởng thì khỏi lo
            int cost = -skill.deltaBudget;
            // // nếu chưa có BudgetController => coi như không đủ tiền để an toàn
            return BudgetController.I != null && BudgetController.I.CanAfford(cost);
        }

        // // API 1: áp dụng theo affectAllAgents trong skill (nhanh gọn)
        public bool ApplySkill(LeaderSkillDefinition skill)
        {
            // // nếu muốn target 1 agent => dùng overload phía dưới
            return ApplySkill(skill, target: null);
        }

        // // API 2: cho phép chỉ định 1 agent (nếu skill.affectAllAgents == false)
        public bool ApplySkill(LeaderSkillDefinition skill, CharacterAgent target)
        {
            if (skill == null) return false;

            // 1) cooldown
            if (!IsReady(skill, out float remain) || remain > 0f)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[LeaderSkill] {skill.skillName} đang cooldown ({remain:0.0}s)");
#endif
                return false;
            }

            // 2) ngân sách
            if (skill.deltaBudget != 0)
            {
                // // an toàn: nếu chưa có BudgetController thì thôi khỏi đụng tiền
                if (BudgetController.I == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning("[LeaderSkill] Chưa có BudgetController => bỏ qua phần cộng/trừ tiền");
#endif
                }
                else if (skill.deltaBudget < 0)
                {
                    int cost = -skill.deltaBudget;
                    if (!BudgetController.I.TrySpend(cost)) return false; // // không đủ tiền => bye
                }
                else
                {
                    BudgetController.I.Add(skill.deltaBudget);
                }
            }

            // 3) tác động tới agent
            if (skill.affectAllAgents)
            {
                var agents = CollectAgents();
                for (int i = 0; i < agents.Count; i++)
                {
                    var a = agents[i];
                    if (!a) continue;
                    var stats = a.GetComponent<CharacterStats>();
                    if (!stats) continue;
                    stats.ApplyDelta(skill.deltaEnergy, skill.deltaStress);
                }
            }
            else
            {
                // // nếu skill chỉ 1 người => dùng target nếu có, không thì lấy agent đầu tiên cho nhanh
                CharacterAgent chosen = target != null ? target : FindFirstObjectByType<CharacterAgent>();
                if (chosen)
                {
                    var stats = chosen.GetComponent<CharacterStats>();
                    if (stats) stats.ApplyDelta(skill.deltaEnergy, skill.deltaStress);
                }
                else
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning("[LeaderSkill] Không tìm thấy agent để áp dụng (affectAllAgents=false)");
#endif
                }
            }

            // 4) set cooldown
            float now = Time.unscaledTime;
            _nextReady[skill] = now + Mathf.Max(0f, skill.cooldownSeconds);

            // 5) popup xinh xinh => gộp text ngắn gọn
            if (popupManager)
            {
                string sE = skill.deltaEnergy != 0 ? (skill.deltaEnergy > 0 ? $"+E{skill.deltaEnergy}" : $"-E{-skill.deltaEnergy}") : "";
                string sS = skill.deltaStress != 0 ? (skill.deltaStress > 0 ? $"+S{skill.deltaStress}" : $"-S{-skill.deltaStress}") : "";
                string sB = skill.deltaBudget != 0 ? (skill.deltaBudget > 0 ? $"+${skill.deltaBudget}" : $"-${-skill.deltaBudget}") : "";
                string joined = $"{skill.skillName}  {sE} {sS} {sB}".Replace("  ", " ").Trim();

                bool mostlyPositive = (skill.deltaBudget > 0) || (skill.deltaStress < 0) || (skill.deltaEnergy > 0);
                popupManager.ShowCenter(joined, mostlyPositive ? popupColorPositive : popupColorNegative);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[LeaderSkill] OK: {skill.skillName} | cd={skill.cooldownSeconds:0.0}s");
#endif
            return true;
        }

        public List<LeaderSkillDefinition> GetRandomPicks(int count)
        {
            var result = new List<LeaderSkillDefinition>();
            if (skillPool == null || skillPool.Count == 0 || count <= 0) return result;

            var temp = new List<LeaderSkillDefinition>(skillPool);
            for (int i = 0; i < count; i++)
            {
                if (temp.Count == 0) temp.AddRange(skillPool);
                int idx = Random.Range(0, temp.Count);
                result.Add(temp[idx]);
                temp.RemoveAt(idx);
            }
            return result;
        }
    }
}
