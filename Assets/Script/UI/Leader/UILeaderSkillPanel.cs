using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Wargency.Gameplay
{
    public class UILeaderSkillPanel : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private LeaderSkillManager leaderSkillManager;
        [SerializeField] private Transform slotsContainer;      // Parent để spawn các slot
        [SerializeField] private UILeaderSkillSlot slotPrefab;  // Prefab 1 ô

        [Header("Optional Actions")]
        [SerializeField] private Button rerollButton;           // có thể để trống
        [SerializeField] private Button closeButton;            // có thể để trống

        [Header("Config")]
        [Min(1)][SerializeField] private int picksPerRoll = 3;  // Số skill hiển thị mỗi lần
        [SerializeField] private bool closeOnApply = false;     //KHÔNG đóng panel sau apply (mặc định false)
        [SerializeField] private bool rerollOnApply = false;    //có reroll sau apply không (mặc định false)

        private readonly List<UILeaderSkillSlot> _spawned = new();

        private void Awake()
        {
            if (rerollButton)
            {
                rerollButton.onClick.RemoveAllListeners();
                rerollButton.onClick.AddListener(DoReroll);
            }
            if (closeButton)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            }
        }

        private void OnEnable()
        {
            BuildSlots(); // mỗi lần mở panel, random lại
        }

        private void OnDisable()
        {
            ClearSlots();
        }

        public void OnSkillApplied()
        {
            // Hành vi sau khi chọn skill: quyết định bằng 2 flag
            if (rerollOnApply) BuildSlots();
            if (closeOnApply) gameObject.SetActive(false);
            // Nếu cả 2 đều false => panel ở nguyên, slot tự hiển thị cooldown.
        }

        private void DoReroll()
        {
            BuildSlots();
        }

        private void BuildSlots()
        {
            if (!leaderSkillManager || !slotPrefab || !slotsContainer) return;

            ClearSlots();

            var picks = leaderSkillManager.GetRandomPicks(picksPerRoll);
            foreach (var def in picks)
            {
                var slot = Instantiate(slotPrefab, slotsContainer);
                slot.Bind(def, leaderSkillManager);
                _spawned.Add(slot);
            }
        }

        private void ClearSlots()
        {
            for (int i = _spawned.Count - 1; i >= 0; i--)
            {
                if (_spawned[i]) Destroy(_spawned[i].gameObject);
            }
            _spawned.Clear();
        }
    }
}
