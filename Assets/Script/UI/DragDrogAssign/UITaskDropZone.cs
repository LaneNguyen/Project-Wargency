using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI; // NEW
using Wargency.Gameplay;

namespace Wargency.UI
{
    // file này là cái vùng để thả agent vào task panel
    // khi thả vào đây sẽ cố gắng assign agent => task instance
    // nếu thành công thì bắn hiệu ứng vui vui và báo alert
    // nếu thất bại thì cũng báo cho biết lý do để người chơi thử lại
    // giao tiếp UI và Gameplay qua UIDragContext và TaskInstance.AssignCharacter

    [RequireComponent(typeof(RectTransform))]
    public class UITaskDropZone : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Refs")]
        [SerializeField] private UITaskPanel panel;
        [SerializeField] private TaskInstance task;

        [Header("Alerts (optional)")]
        [SerializeField] private UILiveAlertsFeed alerts;

        [Header("Effects")]
        [SerializeField] private GameObject successEffectPrefab;
        [SerializeField] private GameObject failEffectPrefab;

        [Header("Hover")]
        [SerializeField] private GameObject highlightGO;

        [Header("Raycast Catcher")]
        [SerializeField] private bool autoAddRaycastCatcher = true; // NEW

        private void Awake()
        {
            if (!panel) panel = GetComponent<UITaskPanel>();
            if (autoAddRaycastCatcher) EnsureRaycastTarget();
        }

        // đảm bảo vùng này bắt được raycast khi kéo thả
        private void EnsureRaycastTarget() // NEW
        {
            // 1) nếu có CanvasGroup thì bật blocksRaycasts
            var cg = GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = true;

            // 2) nếu chưa có Graphic để bắt raycast => thêm Image trong suốt
            var g = GetComponent<Graphic>();
            if (g == null)
            {
                var img = gameObject.AddComponent<Image>();
                img.raycastTarget = true;
                // dùng alpha rất nhỏ để tránh 1 số case alpha-hit-test
                img.color = new Color(0f, 0f, 0f, 0.001f);
            }
            else
            {
                g.raycastTarget = true;
            }
        }

        // cho hệ khác bind task vô đây cho gọn
        public void Bind(TaskInstance ti) => task = ti;

        // hover vô thì sáng viền lên cho người chơi biết thả vào đây nè
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (highlightGO) highlightGO.SetActive(true);
        }

        // rời ra thì tắt sáng
        public void OnPointerExit(PointerEventData eventData)
        {
            if (highlightGO) highlightGO.SetActive(false);
        }

        // khi thả agent vào đây => thử assign
        // UI và Gameplay: lấy agent từ UIDragContext và gọi task.AssignCharacter
        public void OnDrop(PointerEventData eventData)
        {
            if (highlightGO) highlightGO.SetActive(false);

            var agent = UIDragContext.CurrentAgent;  // được set trong UICharacterDraggable
            if (agent == null || task == null) return;

            var result = task.AssignCharacter(agent);
            if (panel) panel.RefreshAssignee(); // UI ⇄ UI: panel tự refresh người nhận

            bool ok = result == AssignResult.Success;
            if (ok)
            {
                if (alerts) alerts.Push($"✅ {agent.DisplayName} đã nhận \"{task.DisplayName}\"");
                SpawnEffect(successEffectPrefab);
            }
            else
            {
                if (alerts) alerts.Push($"⚠️ Không thể giao \"{task.DisplayName}\" cho {agent.DisplayName} (role mismatch?)");
                SpawnEffect(failEffectPrefab);
            }
            AudioManager.Instance.PlaySE(AUDIO.SE_PLOPEFFECT);
            AudioManager.Instance.PlaySE("FlopEffect");
            // UI ⇄ Manager: thả xong thì trả con trỏ về mặc định để khỏi bị nhầm
            if (CursorManager.Instance != null) CursorManager.Instance.SetDefaultCursor();
        }

        // tiện ích sinh hiệu ứng ở đúng chỗ
        private void SpawnEffect(GameObject prefab)
        {
            if (!prefab) return;
            var root = panel ? panel.EffectAnchor : (transform as RectTransform);
            var go = Instantiate(prefab, root);
            go.transform.localPosition = Vector3.zero;
        }
    }
}
