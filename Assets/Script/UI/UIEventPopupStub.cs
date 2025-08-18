using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wargency.Gameplay;

namespace Wargency.UI
{
    // UI debug đơn giản: hiển thị popup Event khi EventManager bắn sự kiện.
    public class UIEventPopupStub : MonoBehaviour
    {
        [Header("Refs")]
        public EventManager eventManager; // tham chiếu đến EventManager để lắng nghe

        [Header("UI")]
        public GameObject panel; // panel chứa popup
        public TextMeshProUGUI titleText; // hiển thị tiêu đề event
        public TextMeshProUGUI descText;  // hiển thị mô tả event
        public Button okButton;           // nút OK để đóng

        void Awake()
        {
            if (panel != null)
                panel.SetActive(false);
            if (okButton != null)
                okButton.onClick.AddListener(Close); //gọi hàm close

            if (eventManager != null)
            {
                eventManager.OnEventTrigger += HandleEventTrigger;
            }
        }

        void OnDestroy()
        {
            if (eventManager != null)
            { eventManager.OnEventTrigger -= HandleEventTrigger;
                Debug.Log("[UIEventPopupStub] Unsubscribed from OnEventTrigger on OnDestroy");
            }
        }

        // Khi EventManager báo sự kiện, cập nhật UI và hiện popup.
        private void HandleEventTrigger(EventDefinition ev)
        {
            if (ev == null)
                return;
            if (titleText != null)
                titleText.text = ev.title;
            if (descText != null)
                descText.text = ev.description;
            if (panel != null)
                panel.SetActive(true);
        }

        // Đóng popup only, còn tiền bạc điểm +- đã được chạy từ EventManager rồi
        public void Close()
        {
            if (panel != null)
                panel.SetActive(false);
        }
    }
}
