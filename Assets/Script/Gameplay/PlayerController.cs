using UnityEngine;
using Wargency.Core;
using UnityEngine.EventSystems; // để chặn click lên UI

namespace Wargency.Gameplay
{
    [RequireComponent(typeof(Rigidbody2D))] // gắn script này thì phải có Rb2D, thiếu là tự thêm cho đỡ quên
    public class PlayerController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private GameConfig config; // lấy tốc độ từ đây cho đỡ cứng code
        [SerializeField] private float moveSpeedMultiplier = 1f;
        [SerializeField] private LayerMask wallCheck;

        [Header("Movement Toggles")]
        [SerializeField] private bool enableKeyboardMove = true; // cho đi bằng phím nè
        [SerializeField] private bool enableClickMove = false;   // mặc định tắt chuột vì chưa ngon, lúc nào cần thì bật

        [Header("Hotkeys")]
        [SerializeField] private KeyCode toggleKeyboardKey = KeyCode.F5; // bấm cái này để tắt/mở đi bằng phím
        [SerializeField] private KeyCode toggleClickKey = KeyCode.F6; // bấm cái này để tắt/mở đi bằng chuột

        private Rigidbody2D rb;
        private Vector2 moveInput;

        private Vector2 clickTarget;
        private bool isMovingClick;

        private Vector2 lastWalltouch;

        private Animator ani;
        private int animoveXID = Animator.StringToHash("moveX");
        private int animoveYID = Animator.StringToHash("moveY");
        private int aniisMoving = Animator.StringToHash("isMoving");

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>(); // xin cái rb về xài
            ani = GetComponentInChildren<Animator>();
            if (rb != null)
            {
                rb.gravityScale = 0f; // top-down thì khỏi rơi
                rb.freezeRotation = true; // khỏi xoay cho đỡ mệt
            }

            clickTarget = rb != null ? rb.position : Vector2.zero;
            isMovingClick = false;
        }

        private void Update()
        {
            // bắt phím để bật/tắt từng mode, để test nhanh khỏi vào Inspector
            if (Input.GetKeyDown(toggleKeyboardKey))
            {
                enableKeyboardMove = !enableKeyboardMove;
                if (!enableKeyboardMove) moveInput = Vector2.zero; // tắt phím thì dừng luôn cho chắc
            }

            if (Input.GetKeyDown(toggleClickKey))
            {
                enableClickMove = !enableClickMove;
                if (!enableClickMove) StopClickMove(); // tắt chuột thì ngừng đi tới target
            }

            // lấy input bàn phím nếu đang bật, còn không thì thôi cho về 0
            if (enableKeyboardMove)
            {
                moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
            }
            else
            {
                moveInput = Vector2.zero;
            }

            // giữ logic click nhưng chỉ chạy khi được bật
            if (enableClickMove)
            {
                HandleClick();
            }
            else
            {
                // đang đi theo click mà user vừa tắt chuột thì dừng lại luôn
                if (isMovingClick) StopClickMove();
            }
        }

        private void FixedUpdate()
        {
            // di chuyển kiểu Rigidbody2D cho mượt
            Vector2 velocity = moveInput;
            float speed = (config != null ? config.playerMoveSpeed : 3.5f) * moveSpeedMultiplier; // có config thì dùng, không thì xài tạm 3.5
            rb.linearVelocity = velocity * speed; // code gốc dùng linearVelocity, thôi mình theo luôn

            AnimationUpdate();
        }

        private void HandleClick()
        {
            // click chuột trái để đặt điểm đến (nếu không trỏ lên UI)
            if (Input.GetMouseButtonDown(0))
            {
                if (IsPointerOverUI()) return; // trỏ lên UI thì thôi khỏi đi

                clickTarget = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                isMovingClick = true;
            }

            // nếu đang đi theo click thì override moveInput cho đi tới đó
            if (isMovingClick)
            {
                float distance = Vector2.Distance(rb.position, clickTarget);
                if (distance < 0.5f) // gần tới rồi thì dừng
                {
                    StopClickMove();
                }

                Vector2 movingDirection = (clickTarget - rb.position).normalized;
                moveInput = movingDirection;
            }
        }

        private void OnCollisionStay2D(Collision2D c)
        {
            lastWalltouch = c.GetContact(0).normal;
            if (c.gameObject.CompareTag("Office Map - Wall"))
            {
                StopClickMove(); // đụng tường thì thôi đừng đẩy nữa
                rb.position += lastWalltouch * 0.01f; // nảy nhẹ cho khỏi kẹt
            }
        }

        private void StopClickMove()
        {
            moveInput = Vector2.zero;
            isMovingClick = false;
            clickTarget = rb.position;
            rb.linearVelocity = Vector2.zero;
        }

        private void AnimationUpdate()
        {
            if (ani != null)
            {
                Vector2 velocity = rb.linearVelocity;

                bool isMoving = velocity.sqrMagnitude > 0.0001f; // hơi nhúc nhích là tính đang đi
                ani.SetBool(aniisMoving, isMoving);

                Vector2 direction = isMoving ? velocity.normalized : Vector2.zero;
                ani.SetFloat(animoveXID, Mathf.Round(direction.x));
                ani.SetFloat(animoveYID, Mathf.Round(direction.y));
            }
        }

        // chặn click khi trỏ lên UI (PC + mobile)
        private bool IsPointerOverUI()
        {
            if (EventSystem.current == null) return false;
            if (EventSystem.current.IsPointerOverGameObject()) return true;
            return false;
        }
    }
}
