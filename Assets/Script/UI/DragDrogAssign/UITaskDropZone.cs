using UnityEngine;
using UnityEngine.EventSystems;
using Wargency.Gameplay;

namespace Wargency.UI
{
    //Gắn vào cái ô task để nhận drop từ UICharacterDraggable

    public class UITaskDropZone : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Refs")]
        [SerializeField] private UITaskPanel panel;     // Panel ra task
        [SerializeField] private TaskInstance task;     // Task instance gắn với panel này 

        [Header("Effects")]
        [SerializeField] private GameObject successEffectPrefab;
        [SerializeField] private GameObject failEffectPrefab;

        [Header("Hover")]
        [SerializeField] private GameObject highlightGO;

        private void Reset()
        {
            panel = GetComponent<UITaskPanel>();
        }

        public void Bind(TaskInstance ti)
        {
            task = ti;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (highlightGO != null) highlightGO.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (highlightGO != null) highlightGO.SetActive(false);
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (highlightGO != null) highlightGO.SetActive(false);

            var agent = UIDragContext.CurrentAgent;
            if (agent == null || task == null) return;

            var result = task.AssignCharacter(agent);
            if (panel != null) panel.RefreshAssignee();

            var prefab = result == AssignResult.Success ? successEffectPrefab : failEffectPrefab;
            if (prefab != null)
            {
                var root = panel != null ? panel.EffectAnchor : transform as RectTransform;
                var go = Instantiate(prefab, root);
                go.transform.localPosition = Vector3.zero;
            }
        }
    }
}
