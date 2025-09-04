using UnityEngine;

// script này làm chữ popup bay lên nhẹ nhàng rồi tự tắt
// độ mờ và khoảng bay dùng animation curve cho mượt mà
// gọi PlayFrom để bắt đầu từ một vị trí trong canvas
// UI => Manager => Gameplay chỉ nhận text và bay chơi thôi

namespace Wargency.Gameplay
{
    [RequireComponent(typeof(CanvasGroup))]
    public class UIPopupFloat : MonoBehaviour
    {
        [SerializeField] private float lifetime = 1.2f;
        [SerializeField] private float riseDistance = 40f; // px trong canvas
        [SerializeField] private AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
        [SerializeField] private AnimationCurve riseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private RectTransform _rt;
        private CanvasGroup _cg;
        private Vector2 _startPos;
        private float _t;

        private void Awake()
        {
            _rt = transform as RectTransform;
            _cg = GetComponent<CanvasGroup>();
        }

        // bắt đầu hiệu ứng bay từ vị trí đã cho trong anchored space


        public void PlayFrom(Vector2 anchoredStart)
        {
            _startPos = anchoredStart;
            _t = 0f;
            if (_rt) _rt.anchoredPosition = _startPos;
            if (_cg) _cg.alpha = 1f;
            enabled = true;
        }

        private void OnEnable()
        {
            _t = 0f;
        }

        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(_t / lifetime);

            // Alpha & vị trí
            if (_cg) _cg.alpha = alphaCurve.Evaluate(u);
            if (_rt) _rt.anchoredPosition = _startPos + Vector2.up * (riseDistance * riseCurve.Evaluate(u));

            if (_t >= lifetime) gameObject.SetActive(false);
        }
    }
}
