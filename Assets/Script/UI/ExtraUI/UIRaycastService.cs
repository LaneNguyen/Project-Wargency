using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIRaycastService : MonoBehaviour
{
    [Header("Raycasters (để trống sẽ tự quét)")]
    [SerializeField] private List<GraphicRaycaster> raycasters = new();

    [Tooltip("Nếu true, Awake() sẽ tự FindObjectsOfType<GraphicRaycaster>(includeInactive: true)")]
    [SerializeField] private bool autoScanOnAwake = true;

    private PointerEventData ped;
    private readonly List<RaycastResult> results = new(16);

    private void Awake()
    {
        if (autoScanOnAwake && (raycasters == null || raycasters.Count == 0))
        {
            // LẤY HẾT GraphicRaycaster trong scene (kể cả inactive)
            var arr = FindObjectsOfType<GraphicRaycaster>(true);
            raycasters = new List<GraphicRaycaster>(arr);
        }

        if (EventSystem.current == null)
        {
            Debug.LogWarning("[UIRaycastService] Không thấy EventSystem trong scene → raycast UI sẽ luôn fail.");
        }
        ped = new PointerEventData(EventSystem.current);
    }

    // Trả về true nếu screenPos đang đè lên bất kỳ phần tử UI nào (có Raycast Target).
    // trả về true nếu chuột đang đụng vô cái gì đó thuộc UI

    public bool IsPointerOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;
        if (raycasters == null || raycasters.Count == 0) return false;

        ped.position = screenPos;      // set vị trí trỏ
        // ped.pointerId = -1;         // không bắt buộc
        // ped.button = PointerEventData.InputButton.Left;

        for (int i = 0; i < raycasters.Count; i++)
        {
            var gr = raycasters[i];
            if (gr == null || !gr.isActiveAndEnabled) continue;

            results.Clear();
            gr.Raycast(ped, results);
            if (results.Count > 0)
                return true; // Bất kỳ UI nào trúng là block
        }
        return false;
    }

    // nếu spawn thêm canvas lúc chạy thì gọi lại để cập nhật danh sách raycaster


    public void RefreshRaycasters()
    {
        var arr = FindObjectsOfType<GraphicRaycaster>(true);
        raycasters = new List<GraphicRaycaster>(arr);
    }
}
