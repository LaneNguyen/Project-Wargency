using System.Collections;
using UnityEngine;

namespace Wargency.UI
{
    // // CursorManager nè
    // // nhiệm vụ: đổi hình con trỏ cho vui mắt (default / đang kéo / vừa thả)
    // // xài Cursor.SetCursor nên không cần Canvas gì hết cho đỡ mệt
    // // có vụ "alt-tab quay lại vẫn giữ đúng con trỏ đang xài" luôn nha

    public class CursorManager : MonoBehaviour
    {
        public static CursorManager Instance { get; private set; }

        private enum CursorState
        {
            Default,    // // bình thường thôi không có gì hot
            Dragging,   // // đang kéo đồ nghe cho ngầu
            Flashing    // // mới thả xong, nháy cái cho biết là xong rồi
        }

        [Header("Default")]
        [Tooltip("ảnh con trỏ bình thường (Texture Type: Cursor, no mipmap)")]
        [SerializeField] private Texture2D defaultCursor;
        [SerializeField] private Vector2 defaultHotspot = new Vector2(2, 2);

        [Header("Dragging")]
        [Tooltip("ảnh con trỏ lúc kéo đồ (cho người chơi biết là đang kéo thật)")]
        [SerializeField] private Texture2D draggingCursor;
        [SerializeField] private Vector2 draggingHotspot = new Vector2(6, 6);

        [Header("Just Released")]
        [Tooltip("ảnh con trỏ vừa thả (hiện tí xíu rồi biến mất)")]
        [SerializeField] private Texture2D releasedCursor;
        [SerializeField] private Vector2 releasedHotspot = new Vector2(2, 2);
        [SerializeField, Min(0f)] private float releasedFlashSeconds = 0.18f;

        [Header("Advanced")]
        [Tooltip("alt-tab quay lại thì giữ đúng con trỏ đang xài (bật cho chắc cú)")]
        [SerializeField] private bool keepCursorOnFocusLost = true;

        private Coroutine flashRoutine;
        private CursorState state = CursorState.Default; // // nhớ trạng thái hiện tại để lúc quay lại còn áp cho chuẩn

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            SetDefaultCursor(); // // vào game thì cứ default đã, an toàn
        }

        public void SetDefaultCursor()
        {
            StopFlashIfAny();
            state = CursorState.Default; // // nhớ là đang default nha
            ApplyCursor(defaultCursor, defaultHotspot);
        }

        public void SetDraggingCursor()
        {
            StopFlashIfAny();
            // // phòng hờ thiếu ảnh kéo thì xài ảnh default luôn, khỏi crash
            var tex = draggingCursor ? draggingCursor : defaultCursor;
            var hs = draggingCursor ? draggingHotspot : defaultHotspot;
            state = CursorState.Dragging; // // nhớ là đang kéo nha
            ApplyCursor(tex, hs);
        }

        // // hiện con trỏ "vừa thả" một lúc rồi tự về default
        public void FlashReleasedCursor()
        {
            StopFlashIfAny();
            flashRoutine = StartCoroutine(CoFlashReleased());
        }

        private IEnumerator CoFlashReleased()
        {
            state = CursorState.Flashing; // // đang nháy, đừng đụng
            var tex = releasedCursor ? releasedCursor : defaultCursor;
            var hs = releasedCursor ? releasedHotspot : defaultHotspot;
            ApplyCursor(tex, hs);

            yield return new WaitForSeconds(releasedFlashSeconds);

            // // hết nháy thì trở lại default cho nhẹ đầu
            state = CursorState.Default;
            ApplyCursor(defaultCursor, defaultHotspot);
            flashRoutine = null;
        }

        private void StopFlashIfAny()
        {
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
                flashRoutine = null;
            }
        }

        private void ApplyCursor(Texture2D tex, Vector2 hotspot)
        {
            // // Texture set đúng kiểu "Cursor" thì Unity tự lo HiDPI
            Cursor.SetCursor(tex, hotspot, CursorMode.Auto);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!keepCursorOnFocusLost) return;

            // // ý tưởng: nếu quay lại game thì áp đúng con trỏ đang nhớ
            // // đang flashing thì thôi kệ, cho nó nháy nốt
            if (hasFocus && flashRoutine == null)
            {
                switch (state)
                {
                    case CursorState.Default:
                        ApplyCursor(defaultCursor, defaultHotspot); break;
                    case CursorState.Dragging:
                        ApplyCursor(draggingCursor ? draggingCursor : defaultCursor,
                                    draggingCursor ? draggingHotspot : defaultHotspot);
                        break;
                        // // flashing thì không tới đây vì có routine đang chạy
                }
            }
        }
    }
}
