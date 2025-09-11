using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wargency.Gameplay;

namespace Wargency.UI
{
    public class UIEventPopupStub : MonoBehaviour
    {
        [Header("Refs")]
        public EventManager eventManager;

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

        [Header("Optional on-panel text")]
        public TextMeshProUGUI teamDeltaTextOnPanel; // có thể để trống

        // 0909 Update: chỗ hiển thị tiền/stress riêng (text thuần, không prefab)
        [Header("Money/Stress")]
        public TextMeshProUGUI moneyDeltaTextOnPanel;  // ví dụ: "+500$" hoặc "-300$"
        public TextMeshProUGUI stressDeltaTextOnPanel; // ví dụ: "+2 Stress" hoặc "-1 Stress"
        [SerializeField] private float autoClearSeconds = 1.6f;

        // ... state cũ
        private bool _lastTriggerWasChoice = false;
        private EventManager.ChoiceEventData _lastChoiceData;

        void Awake()
        {
            if (panelInstant) panelInstant.SetActive(false);
            if (panelChoice) panelChoice.SetActive(false);

            if (okButton != null) okButton.onClick.AddListener(CloseInstant);
            if (buttonA != null) buttonA.onClick.AddListener(() => OnClickChoice(true));
            if (buttonB != null) buttonB.onClick.AddListener(() => OnClickChoice(false));

            if (eventManager != null)
            {
                eventManager.OnEventTrigger += HandleEventTrigger;
                eventManager.OnEventText += HandleEventText;
                eventManager.OnChoiceEvent += HandleChoiceEvent;

                // 0909 Update: subscribe sự kiện mới có payload
                eventManager.OnEventResolvedMoneyStress += HandleEventResolvedMoneyStress;
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

                // 0909 Update: unsubscribe
                eventManager.OnEventResolvedMoneyStress -= HandleEventResolvedMoneyStress;

                Debug.Log("[UIEventPopupStub] Unsubscribed from EventManager events on OnDestroy");
            }
        }

        // ====== Instant flow ======
        private void HandleEventTrigger(EventDefinition ev)
        {
            if (ev == null) return;

            if (_lastTriggerWasChoice)
            {
                _lastTriggerWasChoice = false;
                return;
            }

            if (panelChoice) panelChoice.SetActive(false);
            if (panelInstant) panelInstant.SetActive(true);

            if (titleText) titleText.text = ev.title;
            if (descText) descText.text = ev.description;

            if (teamDeltaTextOnPanel) teamDeltaTextOnPanel.text = string.Empty;

            // 0909 Update: clear money/stress text khi mở panel mới
            if (moneyDeltaTextOnPanel) moneyDeltaTextOnPanel.text = string.Empty;
            if (stressDeltaTextOnPanel) stressDeltaTextOnPanel.text = string.Empty;
        }

        public void CloseInstant()
        {
            if (panelInstant) panelInstant.SetActive(false);
        }

        // ====== Choice flow ======
        private void HandleChoiceEvent(EventManager.ChoiceEventData data)
        {
            _lastTriggerWasChoice = true;
            _lastChoiceData = data;

            if (panelInstant) panelInstant.SetActive(false);
            if (panelChoice) panelChoice.SetActive(true);

            if (choiceTitleText) choiceTitleText.text = data.title;
            if (choiceDescText) choiceDescText.text = data.description;

            if (optionATitleText) optionATitleText.text = string.IsNullOrWhiteSpace(data.optionATitle) ? "Option A" : data.optionATitle;
            if (optionADescText) optionADescText.text = data.optionADesc ?? "";

            if (optionBTitleText) optionBTitleText.text = string.IsNullOrWhiteSpace(data.optionBTitle) ? "Option B" : data.optionBTitle;
            if (optionBDescText) optionBDescText.text = data.optionBDesc ?? "";

            if (teamDeltaTextOnPanel) teamDeltaTextOnPanel.text = string.Empty;
            if (moneyDeltaTextOnPanel) moneyDeltaTextOnPanel.text = string.Empty;
            if (stressDeltaTextOnPanel) stressDeltaTextOnPanel.text = string.Empty;
        }

        private void OnClickChoice(bool chooseA)
        {
            if (eventManager == null || _lastChoiceData.root == null)
            {
                Debug.LogWarning("[UIEventPopupStub] OnClickChoice: thiếu eventManager hoặc root.");
                return;
            }

            eventManager.ApplyChoice(_lastChoiceData.root, chooseA);

            if (panelChoice) panelChoice.SetActive(false);
        }

        // ====== Team delta text cũ ======
        private void HandleEventText(string text)
        {
            var feed = FindFirstObjectByType<UILiveAlertsFeed>();
            if (feed != null && !string.IsNullOrEmpty(text))
            {
                feed.Push(text);
            }
            if (teamDeltaTextOnPanel != null)
            {
                teamDeltaTextOnPanel.text = text ?? string.Empty;
            }
        }

        // ====== 0909 Update: nhận (money, stress) và gán vào text ======
        private void HandleEventResolvedMoneyStress(int moneyDelta, int stressDelta)
        {
            if (moneyDeltaTextOnPanel != null)
            {
                moneyDeltaTextOnPanel.gameObject.SetActive(true);
                moneyDeltaTextOnPanel.text = (moneyDelta > 0) ? $"+{moneyDelta}$" : (moneyDelta < 0 ? $"{moneyDelta}$" : "");
                if (autoClearSeconds > 0) StartCoroutine(HideLater(moneyDeltaTextOnPanel.gameObject, autoClearSeconds));
            }

            if (stressDeltaTextOnPanel != null)
            {
                stressDeltaTextOnPanel.gameObject.SetActive(true);
                stressDeltaTextOnPanel.text = (stressDelta > 0) ? $"+{stressDelta} Stress" : (stressDelta < 0 ? $"{stressDelta} Stress" : "");
                if (autoClearSeconds > 0) StartCoroutine(HideLater(stressDeltaTextOnPanel.gameObject, autoClearSeconds));
            }
        }
        private System.Collections.IEnumerator HideLater(GameObject go, float sec)
        {
            yield return new WaitForSecondsRealtime(sec);
            if (go != null) go.SetActive(false);
        }
    }
}
