using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wargency.Gameplay;

namespace Wargency.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class UITaskDropZone : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Refs")]
        [SerializeField] private UITaskPanel panel;            // sẽ auto-wire nếu để trống
        [SerializeField] private TaskInstance task;

        [Header("Alerts (optional)")]
        [SerializeField] private UILiveAlertsFeed alerts;

        [Header("Effects")]
        [SerializeField] private GameObject successEffectPrefab;
        [SerializeField] private GameObject failEffectPrefab;

        [Header("Hover")]
        [SerializeField] private GameObject highlightGO;

        [Header("Raycast Catcher")]
        [SerializeField] private bool autoAddRaycastCatcher = true;

        private void Awake()
        {
            // Auto wire panel: cùng object → parent → children
            if (!panel) panel = GetComponent<UITaskPanel>();
            if (!panel) panel = GetComponentInParent<UITaskPanel>();
            if (!panel) panel = GetComponentInChildren<UITaskPanel>(true);

            if (autoAddRaycastCatcher) EnsureRaycastTarget();
        }

        private void EnsureRaycastTarget()
        {
            var cg = GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = true;

            var g = GetComponent<Graphic>();
            if (g == null)
            {
                var img = gameObject.AddComponent<Image>();
                img.raycastTarget = true;
                img.color = new Color(0f, 0f, 0f, 0.001f);
            }
            else g.raycastTarget = true;
        }

        public void Bind(TaskInstance ti) => task = ti;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (highlightGO) highlightGO.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (highlightGO) highlightGO.SetActive(false);
        }


        public void OnDrop(PointerEventData eventData)
        {
            if (highlightGO) highlightGO.SetActive(false);

            var agent = UIDragContext.CurrentAgent;
            if (agent == null || task == null)
            {
                Debug.LogWarning("[DropZone] Agent hoặc Task null -> bỏ qua.");
                return;
            }

            // === CHECK ROLE TRƯỚC KHI ASSIGN ===
            var def = task.Definition;
            bool hasRestriction = def != null && def.UseRequiredRole; // theo TaskDefinition
            bool roleMismatch = hasRestriction && (agent.Role != def.RequiredRole);

            if (roleMismatch)
            {
                Debug.Log($"[DropZone] ROLE_MISMATCH -> cần {def.RequiredRole}, nhưng {agent.DisplayName} là {agent.Role}");
                if (alerts) alerts.Push($"⚠️ Sai role: \"{task.DisplayName}\" cần {def.RequiredRole}, nhưng {agent.DisplayName} là {agent.Role}");

                if (panel) panel.ShowMismatchWarning(0.9f);            // bật icon + nháy
                if (panel && failEffectPrefab) panel.PlayEffect(failEffectPrefab);

                if (AudioManager.Instance != null) AudioManager.Instance.PlaySE(AUDIO.SE_WRONG);
                if (CursorManager.Instance != null) CursorManager.Instance.SetDefaultCursor();
                return;                                                 // CHẶN assign khi sai role
            }

            // === Role hợp lệ -> thử assign ===
            var result = task.AssignCharacter(agent);
            if (panel) panel.RefreshAssignee();

            if (result == AssignResult.Success)
            {
                if (alerts) alerts.Push($"✅ {agent.DisplayName} đã nhận \"{task.DisplayName}\"");
                if (panel && successEffectPrefab) panel.PlayEffect(successEffectPrefab);
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySE(AUDIO.SE_PLOPEFFECT); // plop cũ
            }
            else
            {
                if (alerts) alerts.Push($"⚠️ Không thể giao \"{task.DisplayName}\" cho {agent.DisplayName}");
                if (panel && failEffectPrefab) panel.PlayEffect(failEffectPrefab);
                if (panel) panel.ShowMismatchWarning(0.9f);
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySE(AUDIO.SE_WRONG);
            }

            if (CursorManager.Instance != null) CursorManager.Instance.SetDefaultCursor();
        }

    }

}
