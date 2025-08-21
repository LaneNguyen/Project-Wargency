using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Wargency.Gameplay
{
    public class HireItemUI : MonoBehaviour
    {
        [Header("UI Refs")]
        [SerializeField] private Image avatarImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI roleText;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button hireButton;

        // runtime backing:
        private CharacterHiringService hiringService;
        private CharacterHiringService.HireOption option;
        private Transform spawnPoint;
        private WaveManager waveManager;

        public void Setup(CharacterHiringService service, CharacterHiringService.HireOption opt, Transform spawn, WaveManager wave = null)
        {
            hiringService = service;
            option = opt;
            spawnPoint = spawn;
            waveManager = wave;

            var def = opt.definition;
            if (def != null)
            {
                if (nameText) nameText.text = def.DisplayName;
                if (roleText) roleText.text = def.RoleTag;
                if (avatarImage) avatarImage.sprite = def.Avatar; 
            }
            if (priceText) priceText.text = $"{opt.hireCost:N0}";

            if (hireButton)
            {
                hireButton.onClick.RemoveAllListeners();
                hireButton.onClick.AddListener(HandleHireClicked);
            }

            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (hiringService == null || option == null) return;

            //lấy wave đem đi kiểm tra
            int waveIdx = waveManager ? waveManager.GetCurrentWaveIndex() : int.MaxValue;
            string reason;
            
            //kiểm tra logic thuê được => lấy lý do ra
            CharacterHiringService.HireOption _;
            bool canLogic = hiringService.CanHireNonBudget(option.definition, waveIdx, out _, out reason);

            // kiểm tra ngân sách hiện tại 
            int budget = GameLoopController.Instance ? GameLoopController.Instance.CurrentBudget : 0;
            bool canBudget = budget >= option.hireCost;

            if (statusText)
            {
                if (!canLogic) statusText.text = reason;           // lock/limit
                else if (!canBudget) statusText.text = "Thiếu Budget";
                else statusText.text = "Sẵn sàng nhích";
            }

            //nếu có nút rồi => đủ logic và budget thì bật lên
            if (hireButton) 
                hireButton.interactable = canLogic && canBudget;
        }

        private void HandleHireClicked()
        {
            if (hiringService == null || option == null || spawnPoint == null) return;

            int waveIdx = waveManager ? waveManager.GetCurrentWaveIndex() : int.MaxValue;
            var agent = hiringService.Hire(option.definition, spawnPoint.position, waveIdx); //

            if (agent == null && statusText) //ko có agent rồi mà vẫn có text
                statusText.text = "Thuê thất bại";
        }
    }
}