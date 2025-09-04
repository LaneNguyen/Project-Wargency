using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Wargency.UI
{
    [RequireComponent(typeof(Button))]
    // nút bật tắt panel HR nè
    // có thể gán backdrop để bấm ra ngoài là đóng lại cho lịch sự
    // nhớ có EventSystem trong scene nha

    public class HRPanelToggle : MonoBehaviour
    {
        [Header("Target panel to show/hide")]
        [SerializeField] private GameObject targetPanel;

        [Header("Start state")]
        [SerializeField] private bool startHidden = true;

        [Header("Optional overlay to close when clicking outside")]
        [SerializeField] private Button backdropCloseButton; // có thể để trống

        private Button _btn;

        private void Awake()
        {
            // 1) Bắt eventclick từ chính Button trên object này
            _btn = GetComponent<Button>();
            if (_btn == null)
            {
                Debug.LogError("[UIPanelToggle] Không tìm thấy Button trên chính object!", this);
                return;
            }
            _btn.onClick.RemoveAllListeners();
            _btn.onClick.AddListener(Toggle);

            // 2) Kiểm tra EventSystem (thiếu là UI không nhận click)
            if (EventSystem.current == null)
            {
                Debug.LogError("[UIPanelToggle] Không có EventSystem trong scene → Add GameObject > UI > Event System.", this);
            }

            // 3) Ẩn/hiện mặc định
            if (targetPanel != null && startHidden) targetPanel.SetActive(false);

            // 4) Nếu có backdrop (nút full-screen mờ), set hành vi đóng panel
            if (backdropCloseButton != null)
            {
                backdropCloseButton.onClick.RemoveAllListeners();
                backdropCloseButton.onClick.AddListener(() =>
                {
                    if (targetPanel != null) targetPanel.SetActive(false);
                });
            }
        }

        public void Toggle()
        {
            if (targetPanel == null)
            {
                Debug.LogError("[UIPanelToggle] Chưa gán targetPanel!", this);
                return;
            }

            bool toActive = !targetPanel.activeSelf;
            targetPanel.SetActive(toActive);
            // Bật/tắt backdrop nếu có
            if (backdropCloseButton != null)
                backdropCloseButton.gameObject.SetActive(toActive);

            Debug.Log($"[UIPanelToggle] {(toActive ? "Open" : "Close")} {targetPanel.name}", this);
        }
    }
}