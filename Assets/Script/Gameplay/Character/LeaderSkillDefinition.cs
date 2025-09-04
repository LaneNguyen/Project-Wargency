using UnityEngine;

namespace Wargency.Gameplay
{
    [CreateAssetMenu(fileName = "LeaderSkill", menuName = "Wargency/LeaderSkill", order = 1)]
    public class LeaderSkillDefinition : ScriptableObject
    {
        // // data skill => UI sẽ lấy tên + icon + cost để show
        [Header("Thông tin")]
        public string skillName;
        [TextArea] public string description;
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
    }
}
