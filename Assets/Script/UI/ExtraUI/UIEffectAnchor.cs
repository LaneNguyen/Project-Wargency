using UnityEngine;

// script này là cái mốc để effect biết xuất hiện ở đâu trên UI
// gắn lên RectTransform rồi gọi effect sinh ra dựa trên mốc này
// không có logic gì phức tạp cho khỏi rối não
// UI => Manager => Gameplay không dính gì luôn nên nhẹ tênh

namespace Wargency.UI
{
    // Gắn script này lên RectTransform  muốn effect xuất hiện
    [DisallowMultipleComponent]
    public class UIEffectAnchor : MonoBehaviour { }
}