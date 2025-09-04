using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wargency.Gameplay;

namespace Wargency.UI
{
    // 1 dòng hiển thị tình trạng 1 objective nè
    // có tiêu đề, có số đếm dạng a/b hoặc chữ OK nếu là điều kiện
    // có icon màu đổi theo trạng thái nếu thích
    public class UIObjectiveRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Image statusIcon; // optional

        [Header("Icons/Colors")]
        [SerializeField] private Color incompleteColor = Color.white;
        [SerializeField] private Color completeColor = Color.green;

        private WaveObjectiveDef _def;

        public void Bind(WaveObjectiveDef def)
        {
            _def = def;
            UpdateProgress(def);
        }

        public void UpdateProgress(WaveObjectiveDef def)
        {
            if (def == null) return;

            if (labelText)
                labelText.SetText(BuildTitle(def));

            if (progressText)
            {
                if (def.targetValue > 0f &&
                    (def.kind == ObjectiveKind.CompleteTasks
                     || def.kind == ObjectiveKind.HireCount
                     || def.kind == ObjectiveKind.ResolveEvents))
                {
                    progressText.SetText($"{Mathf.FloorToInt(def.currentValue)} / {Mathf.FloorToInt(def.targetValue)}");
                }
                else if (def.kind == ObjectiveKind.KeepStressBelow
                      || def.kind == ObjectiveKind.KeepEnergyAbove
                      || def.kind == ObjectiveKind.ReachBudget
                      || def.kind == ObjectiveKind.ReachScore)
                {
                    progressText.SetText(def.completed ? "OK" : "…");
                }
                else
                {
                    progressText.SetText(def.completed ? "OK" : "…");
                }
            }

            if (statusIcon)
                statusIcon.color = def.completed ? completeColor : incompleteColor;
        }

        private string BuildTitle(WaveObjectiveDef def)
        {
            if (!string.IsNullOrWhiteSpace(def.displayName))
                return def.displayName;

            int tgt = Mathf.FloorToInt(def.targetValue);
            switch (def.kind)
            {
                case ObjectiveKind.CompleteTasks:
                    return tgt > 0 ? $"Hoàn thành {tgt} nhiệm vụ" : "Hoàn thành nhiệm vụ";
                case ObjectiveKind.HireCount:
                    return tgt > 0 ? $"Thuê {tgt} nhân sự" : "Thuê nhân sự";
                case ObjectiveKind.ResolveEvents:
                    return tgt > 0 ? $"Xử lý {tgt} sự kiện" : "Xử lý sự kiện";
                case ObjectiveKind.KeepStressBelow:
                    return $"Giữ Stress toàn đội ≤ {tgt}";
                case ObjectiveKind.KeepEnergyAbove:
                    return $"Giữ Energy toàn đội ≥ {tgt}";
                case ObjectiveKind.ReachBudget:
                    return $"Đạt Budget ≥ {tgt}";
                case ObjectiveKind.ReachScore:
                    return $"Đạt Score ≥ {tgt}";
                default:
                    return def.kind.ToString();
            }
        }
    }
}