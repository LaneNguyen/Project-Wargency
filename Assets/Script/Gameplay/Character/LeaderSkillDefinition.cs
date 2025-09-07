using UnityEngine;
using System.Collections.Generic; // // để xài List<string> nha

namespace Wargency.Gameplay
{
    [CreateAssetMenu(fileName = "LeaderSkill", menuName = "Wargency/LeaderSkill", order = 1)]
    public class LeaderSkillDefinition : ScriptableObject
    {
        // // data skill => UI sẽ lấy tên + icon + cost để show
        [Header("Thông tin")]
        public string skillName;
        [TextArea] public string description; // // narrative cho vui (có thể không dùng nếu auto-gen)
        public Sprite icon;

        // // bấm là ăn liền => đổi stat + tiền
        [Header("Tác động (áp dụng ngay khi kích hoạt)")]
        public int deltaEnergy;   // + tăng / - giảm Energy (mỗi agent)
        public int deltaStress;   // + tăng / - giảm Stress (mỗi agent)
        public int deltaBudget;   // + cộng / - trừ tiền team (âm = chi phí)

        [Header("Phạm vi")]
        public bool affectAllAgents = true; // // nếu false => chỉ 1 agent (Manager sẽ lo chọn)

        [Header("Cooldown")]
        [Min(0f)] public float cooldownSeconds = 10f;

        // // helper cho UI: show cost/reward nhanh gọn
        public int Cost => deltaBudget < 0 ? -deltaBudget : 0;
        public int Reward => deltaBudget > 0 ? deltaBudget : 0;

        // // =========================================
        // // AUTO DESCRIPTION (hướng #2)
        // // Ghép các hiệu ứng khác nhau thành 1 dòng mô tả dễ đọc
        // // Ví dụ: "+200$, -10 Stress, +20 Energy • All agents • CD 15s"
        // // =========================================
        public string BuildAutoEffectDescription()
        {
            var parts = new List<string>();

            // // tiền trước cho dễ nhìn
            if (deltaBudget != 0)
                parts.Add($"{(deltaBudget > 0 ? "+" : "")}{deltaBudget}$");

            // // năng lượng & stress
            if (deltaEnergy != 0)
                parts.Add($"{(deltaEnergy > 0 ? "+" : "")}{deltaEnergy} Energy");

            if (deltaStress != 0)
                parts.Add($"{(deltaStress > 0 ? "+" : "")}{deltaStress} Stress");

            // // nếu chưa có gì thì ghi "No direct stat change"
            string main = parts.Count > 0 ? string.Join(", ", parts) : "No direct stat change";

            // // phạm vi áp dụng
            string scope = affectAllAgents ? "All agents" : "One agent";

            // // cooldown hiển thị gọn gàng
            string cd = cooldownSeconds > 0f ? $"CD {cooldownSeconds:0.#}s" : "No CD";

            return $"{main} • {scope} • {cd}";
        }

        // // property nhỏ xinh nếu muốn bind nhanh trong UI
        public string AutoEffectDescription => BuildAutoEffectDescription();
    }
}
