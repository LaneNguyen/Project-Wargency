using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Wargency.Gameplay
{
    // Item UI để thuê 1 nhân vật
    // - Bind: Avatar, Name, Role, Price, Description
    // - Nút Hire chỉ sáng khi qua CanHireNonBudget + đủ budget (theo BudgetController)
    // - Tự yêu cầu panel gỡ item khi đã đạt LIMIT hoặc thuê thành công

    public class HireCharacterUI : MonoBehaviour
    {
        [Header("UI Refs")]
        [SerializeField] private Image avatarImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI roleText;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Button hireButton;

        [Header("Optional")]
        [SerializeField] private Transform spawnPoint;     // có thể để trống
        [SerializeField] private WaveManager waveManager;  // optional

        [Header("Budget Source (Optional)")]
        [Tooltip("Kéo thả BudgetController đang dùng. Nếu để trống, script sẽ tự tìm trong scene, sau đó fallback qua singleton BudgetController.I")]
        [SerializeField] private BudgetController budgetController;

        //Update 0905
        [Header("Hire VFX")]
        [SerializeField] private ParticleSystem confettiPrefab;
        [SerializeField] private Vector3 vfxOffset = new Vector3(0f, 1.0f, 0f);
        [SerializeField, Min(0.2f)] private float vfxLifetime = 2.5f;
        [SerializeField] private bool parentVfxToAgent = true;

        // Runtime backing
        private CharacterHiringService hiringService;
        private CharacterHiringService.HireOption option;
        private CharacterDefinition def;

        // Cho Panel lắng nghe để remove
        public System.Action<HireCharacterUI> OnRequestRemove;

        private void Awake()
        {
            // Tự tìm BudgetController nếu chưa gán trong Inspector
            if (budgetController == null)
            {
#if UNITY_2023_1_OR_NEWER || UNITY_2022_1_OR_NEWER
                budgetController = FindAnyObjectByType<BudgetController>();
#else
                budgetController = FindObjectOfType<BudgetController>();
#endif
            }

            // Tự tìm WaveManager nếu chưa gán
            if (waveManager == null)
            {
#if UNITY_2023_1_OR_NEWER || UNITY_2022_1_OR_NEWER
                waveManager = FindAnyObjectByType<WaveManager>();
#else
                waveManager = FindObjectOfType<WaveManager>();
#endif
            }
        }

        public void Setup(CharacterHiringService service, CharacterHiringService.HireOption opt, Transform spawn = null, WaveManager wave = null)
        {
            hiringService = service != null ? service :
#if UNITY_2023_1_OR_NEWER || UNITY_2022_1_OR_NEWER
                FindAnyObjectByType<CharacterHiringService>();
#else
                FindObjectOfType<CharacterHiringService>();
#endif
            option = opt;
            spawnPoint = spawn;
            if (wave != null) waveManager = wave;

            def = option != null ? option.definition : null;

            BindStatic();
            WireButton();
            Refresh(); // chạy 1 lần ngay
        }

        private void OnEnable()
        {
            // Nếu có BudgetController, listen event để Refresh UI ngay khi tiền thay đổi
            var bc = GetBudgetController();
            if (bc != null) bc.OnBudgetChanged += HandleBudgetChanged;
        }

        private void OnDisable()
        {
            var bc = GetBudgetController();
            if (bc != null) bc.OnBudgetChanged -= HandleBudgetChanged;
        }

        private void HandleBudgetChanged(int _)
        {
            Refresh();
        }

        private void Update() => Refresh(); // vẫn giữ Refresh theo frame để an toàn (có thể bỏ nếu muốn tối ưu)

        private void BindStatic()
        {
            if (def != null)
            {
                if (nameText) nameText.text = def.DisplayName;
                if (roleText) roleText.text = def.Role.ToString();
                if (avatarImage) avatarImage.sprite = def.Avatar;
                if (descriptionText) descriptionText.text = def.Description;
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
            bool canLogic = hiringService.CanHireNonBudget(def, waveIdx, out _, out reason);

            // Nếu đã đạt LIMIT/quota -> yêu cầu panel remove item này
            if (!canLogic && LooksLikeLimit(reason))
            {
                OnRequestRemove?.Invoke(this);
                return;
            }

            // 2) Check budget hiện tại bằng BudgetController (CanAfford / Balance)
            var bc = GetBudgetController();
            bool canBudget = bc != null && bc.CanAfford(def.HireCost);

            if (hireButton)
                hireButton.interactable = canLogic && canBudget;
        }

        private BudgetController GetBudgetController()
        {
            // Ưu tiên: serialized ref / auto-found
            if (budgetController != null) return budgetController;

            // Fallback singleton theo thiết kế của BudgetController
            if (BudgetController.I != null) return BudgetController.I;

            return null;
        }

        private int GetCurrentBalance()
        {
            var bc = GetBudgetController();
            return bc != null ? Mathf.Max(0, bc.Balance) : 0;
        }

        private bool LooksLikeLimit(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return false;
            var r = reason.ToLowerInvariant();
            return r.Contains("giới hạn") || r.Contains("limit") || r.Contains("quota") || r.Contains("đã đạt");
        }

        // click nút thuê nè => gọi HiringService để trừ tiền và spawn agent
        private void HandleHireClicked()
        {
            if (hireButton) hireButton.interactable = false;
            if (hiringService == null || def == null) return;

            int waveIdx = waveManager ? waveManager.GetCurrentWaveIndex() : int.MaxValue;
            CharacterAgent agent = spawnPoint
                ? hiringService.Hire(def, spawnPoint.position, waveIdx)
                : hiringService.Hire(def, waveIdx);

            if (agent == null && hireButton) hireButton.interactable = true;

            if (agent != null)
            {
                PlayConfetti(agent.transform);
                OnRequestRemove?.Invoke(this);
            }
        }

        private void PlayConfetti(Transform agentRoot)
        {
            if (!confettiPrefab || agentRoot == null) return;

            Vector3 pos = agentRoot.position + vfxOffset;
            var confetti = Instantiate(confettiPrefab, pos, Quaternion.identity);
            if (parentVfxToAgent) confetti.transform.SetParent(agentRoot, true);

            confetti.Play();
            Destroy(confetti.gameObject, vfxLifetime);
        }
    }
}
