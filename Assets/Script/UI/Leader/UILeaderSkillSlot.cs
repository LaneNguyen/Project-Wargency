using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Wargency.Gameplay
{
    // một ô nhỏ xinh để hiện 1 skill
    // có icon tên và con số tiền +/- cho vui mắt
    // bấm apply thì hỏi manager xem xài được không => nếu được thì báo panel đã xong
    // cooldown hiển thị bằng fill chạy về 0 nhìn rất là chill
    // UI => Manager => Gameplay thông qua LeaderSkillManager nên giữ tham chiếu đừng để null nha

    public class UILeaderSkillSlot : MonoBehaviour
    {
        [Header("UI Refs")]
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text costOrRewardText;
        [SerializeField] private Button applyButton;

        [Header("Details UI")]
        [SerializeField] private TMP_Text descriptionText; //mô tả auto từ dữ liệu skill

        [Header("Cooldown UI")]
        [SerializeField] private Image cooldownFill;   // Image type=Filled (Radial/Horizontal) dùng làm progress cooldown

        private LeaderSkillDefinition _skill;
        private LeaderSkillManager _mgr;


        // bind dữ liệu vào slot cho tươi mới
        public void Bind(LeaderSkillDefinition skill, LeaderSkillManager manager)
        {
            _skill = skill;
            _mgr = manager;

            if (titleText) titleText.text = skill ? skill.skillName : "-";
            if (icon) icon.sprite = skill ? skill.icon : null;

            // hiện chi phí hay phần thưởng theo $ cho dễ hiểu (giữ nguyên logic cũ)
            if (costOrRewardText)
            {
                if (skill == null) costOrRewardText.text = "";
                else if (skill.deltaBudget < 0)
                    costOrRewardText.text = $"-{(-skill.deltaBudget)}$";
                else if (skill.deltaBudget > 0)
                    costOrRewardText.text = $"+{(skill.deltaBudget)}$";
                else
                    costOrRewardText.text = "0$";
            }

            //mô tả tự sinh tổng hợp các hiệu ứng (budget/energy/stress + scope + cooldown)
            if (descriptionText)
            {
                descriptionText.text = skill != null ? skill.AutoEffectDescription : "";
            }

            if (applyButton)
            {
                applyButton.onClick.RemoveAllListeners();
                applyButton.onClick.AddListener(OnClickApply);
            }

            RefreshInteractable();
            RefreshCooldownUI();
        }

        private void Update()
        {
            // cập nhật cooldown và trạng thái bấm được theo thời gian thật
            RefreshCooldownUI();
            RefreshInteractable();
        }

        // nút có bấm được không => vừa sẵn sàng vừa đủ tiền thì ok
        public void RefreshInteractable()
        {
            if (!applyButton || _skill == null || _mgr == null) return;

            bool ready = _mgr.IsReady(_skill, out _);
            bool canPay = _mgr.CanAfford(_skill);
            applyButton.interactable = ready && canPay;
        }

        // cập nhật fill cooldown cho slot
        public void RefreshCooldownUI()
        {
            if (_skill == null || _mgr == null) return;

            float remain = _mgr.GetRemaining(_skill);
            bool onCd = remain > 0.01f;

            if (cooldownFill)
            {
                // đang cooldown thì hiện hình và fillAmount chạy về 0
                cooldownFill.gameObject.SetActive(onCd);
                if (onCd)
                {
                    float total = Mathf.Max(0.01f, _skill.cooldownSeconds);
                    cooldownFill.fillAmount = Mathf.Clamp01(remain / total);
                }
            }
        }

        // bấm apply => nhờ manager áp dụng skill
        public void OnClickApply()
        {
            if (_skill == null || _mgr == null) return;

            bool ok = _mgr.ApplySkill(_skill); // UI => Manager nhờ thực thi skill
            if (!ok) return;

            var panel = GetComponentInParent<UILeaderSkillPanel>();
            if (panel) panel.OnSkillApplied(); // báo cho panel biết là xong 1 cú nhấn
        }
    }
}
