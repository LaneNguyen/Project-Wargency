using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField]
    private float moveSpeed = 5f;

    private bool isMoving = false;
    private Vector3 targetPosition;
    private Animator animator;
    private Rigidbody2D rb;
    void Start()
    {
        animator= GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        targetPosition = transform.position;
    }

    void Update()
    {
        HandleClick();
    }

    void FixedUpdate()
    {
        MoveToTarget();
        UpdateAnimation();

    }

    void HandleClick()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0;
            targetPosition = mouseWorldPos;
            isMoving = true;
        }
    }

    void MoveToTarget()
    {
        if(!isMoving)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        Vector2 direction = (targetPosition - transform.position).normalized;
        rb.linearVelocity = direction * moveSpeed;

        //Nếu gần đến nơi thì dừng lại
        if (Vector3.Distance(transform.position, targetPosition) < 0.5f)
        {
            isMoving = false;
            rb.linearVelocity = Vector3.zero;
        }
        else
        { 
        }
    }

    void UpdateAnimation()
    {
        Vector3 moveDirection = targetPosition - transform.position;
        Vector2 moveDir2D = new Vector2(moveDirection.x,moveDirection.y);

        if (isMoving)
        {
            animator.SetBool("IsMoving", true);
            animator.SetFloat("MoveX", Mathf.Round(moveDir2D.x));
            animator.SetFloat("MoveY", Mathf.Round(moveDir2D.y));
            Debug.Log($"[ANIM] IsMoving = {isMoving}, MoveX = {Mathf.Round(moveDir2D.x)}, MoveY = {Mathf.Round(moveDir2D.y)}");

        }
        else
        {
            animator.SetBool("IsMoving", false);
            animator.SetFloat("MoveX", 0);
            animator.SetFloat("MoveY", 0);
        }


    }
}
