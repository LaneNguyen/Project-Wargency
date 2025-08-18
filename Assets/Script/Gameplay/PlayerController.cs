using UnityEngine;
using System;
using UnityEngine.UIElements;
using Wargency.Core;

namespace Wargency.Gameplay
{
    [RequireComponent(typeof(Rigidbody2D))]//GameObject nào gắn script này cũng phải có Rb2D, nếu chưa có thì tự động gán
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private GameConfig config; //Tham chiếu đến file config để lấy thông số game
        [SerializeField] private float moveSpeedMultiplier = 1f;
        [SerializeField] private LayerMask wallCheck;

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
            rb = GetComponent<Rigidbody2D>(); // lấy component từ object bỏ vào 
            ani = GetComponentInChildren<Animator>();
            if (rb != null)
            {
                rb.gravityScale = 0f; //tắt trọng lực
                rb.freezeRotation = true; //khóa xoay khi va chạm
            }
        }

        private void Update()
        {
            //test kiểu input trc, sau swap thành khác
            moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
            HandleClick();

        }
        private void FixedUpdate()
        {
            Vector2 velocity;
            velocity = moveInput; // Fallback: top-down style
            float speed = (config != null ? config.playerMoveSpeed : 3.5f) * moveSpeedMultiplier; // tính tốc độ từ file config hoặc 3.5f nếu ko có file
            rb.linearVelocity = velocity * speed; // gán vận tốc vào rb

            AnimationUpdate();
        }
        private void HandleClick()
        {
            
            if (Input.GetMouseButtonDown(0))
            {
                clickTarget = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                isMovingClick = true;
            }

            if (isMovingClick)
            {
                float distance = Vector2.Distance(rb.position, clickTarget);
                if (distance < 0.5f) //check khoảng cách nếu ít quá thì dừng
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
                StopClickMove();
                rb.position += lastWalltouch * 0.01f;
            }
        }

        private void StopClickMove()
        {
            moveInput = Vector2.zero;
            isMovingClick = false;
            clickTarget = rb.position;
            rb.linearVelocity = Vector2.zero;
            return;
        }
        private void AnimationUpdate()
        {
            if (ani != null)
            {
                Vector2 velocity = rb.linearVelocity;

                bool isMoving = velocity.sqrMagnitude > 0.0001f; //nếu lớn hơn số kia thì là true
                ani.SetBool(aniisMoving, isMoving);

                Vector2 direction = isMoving ? velocity.normalized : Vector2.zero;
                ani.SetFloat(animoveXID, Mathf.Round(direction.x));
                ani.SetFloat(animoveYID, Mathf.Round(direction.y));

            }

        }

    }
}
