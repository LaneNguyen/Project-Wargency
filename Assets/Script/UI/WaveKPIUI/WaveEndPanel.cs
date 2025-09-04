using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Wargency.UI
{
    // Panel tổng kết cuối wave: hiện số dư cuối, delta (lời/lỗ) và mô tả wave
    // Delta = End - Start (Start được snapshot ngay khi bắt đầu wave).
    // WaveManager đã compose mô tả và truyền vào SetupWaveResult(...).

    [DisallowMultipleComponent]
    public class WaveEndPanel : MonoBehaviour
    {
        [Header("UI Refs")]
        [SerializeField] private GameObject root;                 // bật/tắt cả panel
        [SerializeField] private TextMeshProUGUI endBalanceText;  // số dư cuối wave
        [SerializeField] private TextMeshProUGUI deltaText;       // lời/lỗ
        [SerializeField] private TextMeshProUGUI verdictText;     // "lời" / "lỗ"
        [SerializeField] private TextMeshProUGUI descriptionText; // *** MÔ TẢ WAVE (đã phục hồi) ***
        [SerializeField] private Button continueButton;           // optional: nút Continue

        [Header("Format")]
        [SerializeField] private string currencyPrefix = "";  // ví dụ: "$"
        [SerializeField] private string positivePrefix = "+";
        [SerializeField] private string negativePrefix = "-";

        // Snapshot startBudget để tính nhanh khi chỉ có earnedDelta
        public static int LastStartBalance { get; private set; }

        // Gọi ở WaveManager.StartWave để lưu mốc so sánh
        public static void SetStartBalance(int startBalance)
        {
            LastStartBalance = startBalance;
        }

        // === API WaveManager gọi khi kết wave ===
        public void SetupWaveResult(int earnedDelta, string description, System.Action onContinue)
        {
            int endBalance = LastStartBalance + earnedDelta;

            if (root) root.SetActive(true);
            Render(endBalance, earnedDelta);

            // *** Phục hồi mô tả ***
            if (descriptionText) descriptionText.text = description;

            // Wire hành vi Continue (nếu có nút)
            if (continueButton != null)
            {
                continueButton.onClick.RemoveAllListeners();
                continueButton.onClick.AddListener(() =>
                {
                    _waitingForContinue = false;
                    onContinue?.Invoke();
                });
            }

            // Cho phép Space/click để tiếp tục, khỏi cần nút cũng được
            _onContinue = onContinue;
            _waitingForContinue = true;
        }

        // Các API cũ vẫn giữ để tương thích
        public void Show(int endBalance, int delta)
        {
            if (root) root.SetActive(true);
            Render(endBalance, delta);
        }

        public void ShowWithStart(int startBalance, int endBalance)
        {
            LastStartBalance = startBalance;
            int delta = endBalance - startBalance;
            if (root) root.SetActive(true);
            Render(endBalance, delta);
        }

        public void ShowNowUsingStoredStart(int endBalance)
        {
            int delta = endBalance - LastStartBalance;
            if (root) root.SetActive(true);
            Render(endBalance, delta);
        }

        public void Close()
        {
            if (root) root.SetActive(false);
            _waitingForContinue = false;
        }

        // —— Internal ——
        private System.Action _onContinue;
        private bool _waitingForContinue;

        private void Update()
        {
            if (!_waitingForContinue) return;

            // Nhấn Space hoặc click chuột trái để tiếp tục
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
            {
                _waitingForContinue = false;
                _onContinue?.Invoke();
            }
        }

        private void Render(int endBalance, int delta)
        {
            if (endBalanceText) endBalanceText.text = $"{currencyPrefix}{endBalance:N0}";

            if (deltaText)
            {
                if (delta >= 0)
                    deltaText.text = $"{positivePrefix}{delta:N0}";
                else
                    deltaText.text = $"{negativePrefix}{Mathf.Abs(delta):N0}";
            }

            if (verdictText)
                verdictText.text = (delta >= 0) ? "lời" : "lỗ";
        }
    }
}
