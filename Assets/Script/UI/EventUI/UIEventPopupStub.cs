using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wargency.Gameplay;

// popup stub cho mấy event xuất hiện trong game
// có 2 kiểu: instant popup với nút OK và choice popup với A/B
// còn hiển thị text +- Energy +-Stress team cho vui nữa
// UI => Manager => Gameplay kết nối qua EventManager

namespace Wargency.UI
{
    // UI tối giản cho Event:
    //- Instant: hiện panel mô tả + nút OK (đóng).
    // - Choice: hiện panel mô tả + 2 lựa chọn (A/B), gọi ApplyChoice khi bấm.
    // - Lắng nghe OnEventText để hiển thị chuỗi “+- Energy/+-Stress team” (đẩy vào Alerts, hoặc optional text nếu có).
    public class UIEventPopupStub : MonoBehaviour
    {
        [Header("Refs")]
        public EventManager eventManager; // Drag từ scene vào

        [Header("Instant Panel (OK)")]
        public GameObject panelInstant;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI descText;
        public Button okButton;

        [Header("Choice Panel (A / B)")]
        public GameObject panelChoice;
        public TextMeshProUGUI choiceTitleText;
        public TextMeshProUGUI choiceDescText;

        public TextMeshProUGUI optionATitleText;
        public TextMeshProUGUI optionADescText;
        public Button buttonA;

        public TextMeshProUGUI optionBTitleText;
        public TextMeshProUGUI optionBDescText;
        public Button buttonB;

        [Header("Optional: hiển thị text team delta ngay trên panel (nếu muốn)")]
        public TextMeshProUGUI teamDeltaTextOnPanel; // có thể để trống

        // State để chặn double-popup (OnEventTrigger chạy ngay trước OnChoiceEvent)
        private bool _lastTriggerWasChoice = false;
        private EventManager.ChoiceEventData _lastChoiceData;

        void Awake()
        {
            // Đảm bảo 2 panel đều tắt ban đầu
            if (panelInstant) panelInstant.SetActive(false);
            if (panelChoice) panelChoice.SetActive(false);

            // Wire button
            if (okButton != null) okButton.onClick.AddListener(CloseInstant);
            if (buttonA != null) buttonA.onClick.AddListener(() => OnClickChoice(true));
            if (buttonB != null) buttonB.onClick.AddListener(() => OnClickChoice(false));

            // Subcribe event
            if (eventManager != null)
            {
                eventManager.OnEventTrigger += HandleEventTrigger;
                eventManager.OnEventText += HandleEventText;
                eventManager.OnChoiceEvent += HandleChoiceEvent;
            }
            else
            {
                Debug.LogWarning("[UIEventPopupStub] eventManager chưa gán (UI sẽ không nhận được event).");
            }
        }

        void OnDestroy()
        {
            if (eventManager != null)
            {
                eventManager.OnEventTrigger -= HandleEventTrigger;
                eventManager.OnEventText -= HandleEventText;
                eventManager.OnChoiceEvent -= HandleChoiceEvent;
                Debug.Log("[UIEventPopupStub] Unsubscribed from EventManager events on OnDestroy");
            }
        }

        // =============== Instant flow (OK) ===============

        private void HandleEventTrigger(EventDefinition ev)
        {
            if (ev == null) return;

            // Nếu ngay sau đó là ChoiceEvent, bỏ qua popup instant để tránh double UI
            if (_lastTriggerWasChoice)
            {
                _lastTriggerWasChoice = false; // reset cờ cho lần sau
                return;
            }

            // Hiện popup instant
            if (panelChoice) panelChoice.SetActive(false);
            if (panelInstant) panelInstant.SetActive(true);

            if (titleText) titleText.text = ev.title;
            if (descText) descText.text = ev.description;

            // Clear optional team delta text trên panel
            if (teamDeltaTextOnPanel) teamDeltaTextOnPanel.text = string.Empty;
        }

        public void CloseInstant()
        {
            if (panelInstant) panelInstant.SetActive(false);
        }

        // =============== Choice flow (A/B) ===============

        private void HandleChoiceEvent(EventManager.ChoiceEventData data)
        {
            _lastTriggerWasChoice = true; // Đánh dấu để HandleEventTrigger bỏ qua instant

            _lastChoiceData = data;

            if (panelInstant) panelInstant.SetActive(false);
            if (panelChoice) panelChoice.SetActive(true);

            if (choiceTitleText) choiceTitleText.text = data.title;
            if (choiceDescText) choiceDescText.text = data.description;

            if (optionATitleText) optionATitleText.text = string.IsNullOrWhiteSpace(data.optionATitle) ? "Option A" : data.optionATitle;
            if (optionADescText) optionADescText.text = data.optionADesc ?? "";

            if (optionBTitleText) optionBTitleText.text = string.IsNullOrWhiteSpace(data.optionBTitle) ? "Option B" : data.optionBTitle;
            if (optionBDescText) optionBDescText.text = data.optionBDesc ?? "";

            // Clear optional team delta text khi mở choice
            if (teamDeltaTextOnPanel) teamDeltaTextOnPanel.text = string.Empty;
        }

        private void OnClickChoice(bool chooseA)
        {
            if (eventManager == null || _lastChoiceData.root == null)
            {
                Debug.LogWarning("[UIEventPopupStub] OnClickChoice: thiếu eventManager hoặc root.");
                return;
            }

            eventManager.ApplyChoice(_lastChoiceData.root, chooseA);

            // Đóng panel choice sau khi chọn
            if (panelChoice) panelChoice.SetActive(false);
        }

        // =============== Team delta text (+- Energy/+-Stress) ===============

        private void HandleEventText(string text)
        {
            // 1) Nếu có feed => đẩy vào feed
            var feed = FindFirstObjectByType<UILiveAlertsFeed>();
            if (feed != null && !string.IsNullOrEmpty(text))
            {
                feed.Push(text);
            }

            // 2) Nếu có chỗ hiển thị trên panel => set text
            if (teamDeltaTextOnPanel != null)
            {
                teamDeltaTextOnPanel.text = text ?? string.Empty;
            }
        }
    }
}
