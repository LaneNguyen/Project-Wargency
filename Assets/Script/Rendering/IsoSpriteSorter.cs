using UnityEngine;
using Wargency.Core;

namespace Wargency.Rendering
{
    // Gắn lên mọi object có SpriteRenderer để tự cập nhật sortingOrder theo Y mỗi frame
    // Mục tiêu: vật ở thấp (gần đáy màn hình) vẽ đè lên vật ở cao
    [RequireComponent(typeof(SpriteRenderer))]
    public class IsoSpriteSorter : MonoBehaviour
    {
        [SerializeField] private float scale = 100f; // cùng hệ với IsoHelper.OrderFromY
        [SerializeField]private SpriteRenderer sr;
        private int lastOrder;
        [SerializeField] private Transform targetTransform; // lấy Y từ đâu, mặc định là this.transform
        //Update 2808 để cộng thêm cho nhân vật khi cần
        [SerializeField] private int orderBias = 0; // ví dụ: -50 để hạ thấp nhân vật


        private void Reset()
        {
            // auto tìm SpriteRenderer con đầu tiên
            if (sr == null)
                sr = GetComponentInChildren<SpriteRenderer>();
            if (targetTransform == null)
                targetTransform = this.transform;
        }
        private void Awake()
        {
            lastOrder = int.MinValue;
        }

        private void FixedUpdate()
        {
            if (sr == null || targetTransform == null) return;

            int order = IsometricHelper.OrderFromY(targetTransform.position.y, scale, orderBias);
            if (order != lastOrder)
            {
                sr.sortingOrder = order;
                lastOrder = order;
            }
        }
    }
}
