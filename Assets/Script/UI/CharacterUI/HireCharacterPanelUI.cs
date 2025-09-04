using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Wargency.Gameplay
{
    // <summary>
    // Item UI để thuê 1 nhân vật
    // - Bind: Avatar, Name, Role, Price, Description
    // - Nút Hire chỉ sáng khi qua CanHireNonBudget + đủ budget
    // - Tự yêu cầu panel gỡ item khi đã đạt LIMIT hoặc thuê thành công
    // </summary>
    // item UI để thuê 1 nhân vật nè
    // bind avatar tên vai trò giá mô tả rồi bật tắt nút hire
    // bấm hire => gọi HiringService trừ tiền và spawn agent, xong báo WaveManager cộng KPI 1 cái

    public class HireCharacterUI : MonoBehaviour
    {
        [Header("UI Refs")]
        [SerializeField] private Image avatarImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI roleText;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private TextMeshProUGUI descriptionText;   // NEW: mô tả từ CharacterDefinition
        [SerializeField] private Button hireButton;

        [Header("Optional")]
        [SerializeField] private Transform spawnPoint;              // có thể để trống
        [SerializeField] private WaveManager waveManager;           // optional

        // Runtime backing
        private CharacterHiringService hiringService;
        private CharacterHiringService.HireOption option;
        private CharacterDefinition def;

        // Cho Panel lắng nghe để remove
        public System.Action<HireCharacterUI> OnRequestRemove;

        // <summary>
        // Chuẩn hoá chữ ký Setup để Panel gọi thống nhất
        // </summary>
        public void Setup(CharacterHiringService service, CharacterHiringService.HireOption opt, Transform spawn = null, WaveManager wave = null)
        {
            hiringService = service != null ? service : FindAnyObjectByType<CharacterHiringService>();
            option = opt;
            spawnPoint = spawn;
            waveManager = wave;

            def = option != null ? option.definition : null;

            BindStatic();
            WireButton();
            Refresh(); // chạy 1 lần ngay
        }

        private void Update() => Refresh();

        private void BindStatic()
        {
            if (def != null)
            {
                if (nameText) nameText.text = def.DisplayName;
                if (roleText) roleText.text = def.Role.ToString();
                if (avatarImage) avatarImage.sprite = def.Avatar;
                if (descriptionText) descriptionText.text = def.Description;   // đồng bộ mô tả
            }

            // Giá chuẩn lấy từ Definition.HireCost
            int cost = def != null ? def.HireCost : 0;
            if (priceText) priceText.text = cost.ToString("N0");
        }

        private void WireButton()
        {
            if (!hireButton) return;
            hireButton.onClick.RemoveAllListeners();
            hireButton.onClick.AddListener(HandleHireClicked);
        }

        private void Refresh()
        {
            if (hiringService == null || option == null || def == null) return;

            int waveIdx = waveManager ? waveManager.GetCurrentWaveIndex() : int.MaxValue;

            // 1) Check logic unlock/limit (KHÔNG dính budget)
            string reason;
            CharacterHiringService.HireOption _;
            bool canLogic = hiringService.CanHireNonBudget(def, waveIdx, out _, out reason);   // :contentReference[oaicite:3]{index=3}

            // Nếu đã đạt LIMIT/quota -> yêu cầu panel remove item này
            if (!canLogic && LooksLikeLimit(reason))
            {
                OnRequestRemove?.Invoke(this);
                return;
            }

            // 2) Check budget hiện tại
            int budget = GameLoopController.Instance ? GameLoopController.Instance.CurrentBudget : 0;
            bool canBudget = def != null && budget >= def.HireCost;

            if (hireButton)
                hireButton.interactable = canLogic && canBudget;
        }

        private bool LooksLikeLimit(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return false;
            var r = reason.ToLowerInvariant();
            return r.Contains("giới hạn") || r.Contains("limit") || r.Contains("quota") || r.Contains("đã đạt");
        }

        // click nút thuê nè => gọi HiringService để trừ tiền và spawn agent
        // nếu ok thì báo WaveManager tăng HireCount và nhờ panel gỡ item cho gọn
        private void HandleHireClicked()
        {
            if (hireButton) hireButton.interactable = false; // fix: chặn double click liên tiếp gây thuê đúp
            if (hiringService == null || def == null)
            {
                Debug.LogWarning("[HireCharacterUI] Missing service/definition.");
                return;
            }

            int waveIdx = waveManager ? waveManager.GetCurrentWaveIndex() : int.MaxValue;

            CharacterAgent agent = null;
            if (spawnPoint)
                agent = hiringService.Hire(def, spawnPoint.position, waveIdx);
            else
                agent = hiringService.Hire(def, waveIdx);

            // nếu fail thì mở nút lại cho user thử tiếp
            if (agent == null && hireButton) hireButton.interactable = true;

            // Thuê thành công => yêu cầu panel gỡ item
            // KPI hirecount sẽ do hệ thống Wave/Objective tự đếm, UI không cộng nữa
            if (agent != null)
            {
                if (waveManager == null) waveManager = FindAnyObjectByType<WaveManager>();

                OnRequestRemove?.Invoke(this);
            }
        }
    }
}