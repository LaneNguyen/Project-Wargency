using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Wargency.Gameplay
{
    public class HireCharacterUI : MonoBehaviour
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
        private Transform spawnPoint;// optional
        private WaveManager waveManager;

        private bool initialized = false;

        public void Setup(CharacterHiringService service, CharacterHiringService.HireOption opt, Transform spawn=null, WaveManager wave = null)
        {
            hiringService = service != null ? service : FindAnyObjectByType<CharacterHiringService>();//gia cố
            option = opt;
            spawnPoint = spawn; //có thể để trống
            waveManager = wave;

            var def = opt.definition;
            if (def != null)
            {
                if (nameText) nameText.text = def.DisplayName;
                if (roleText) roleText.text = def.Role.ToString();
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
            // log chẩn đoán
            Debug.Log($"[HireUI] Click. service={hiringService != null}, option={(option != null)}, def={(option != null ? option.definition != null : false)}, spawn={(spawnPoint != null)}");

            if (hiringService == null || option == null)
            {
                if (statusText) statusText.text = "Thiếu gì đó: hiringservice hoặc option";
                Debug.LogWarning("[HireUI] Missing HiringService/Option.");
                return;
            }

            int waveIdx = waveManager ? waveManager.GetCurrentWaveIndex() : int.MaxValue;

            CharacterAgent agent = null;
            if (spawnPoint != null)
                agent = hiringService.Hire(option.definition, spawnPoint.position, waveIdx);
            else
                agent = hiringService.Hire(option.definition, waveIdx); /// dùng SpawnPointSets



            if (agent == null)
            {
                if (statusText) statusText.text = "Thuê thất bại";
                Debug.LogWarning("[HireUI] Hire failed. Kiểm tra: Definition prefab variants hoặc fallback prefab trong HiringService.");
            }
        }
    }
}