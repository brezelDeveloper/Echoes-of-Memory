using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpPower = 5f;
    [SerializeField] private float groundCheckDistance = 1.1f;
    [SerializeField] private float crouchingHeight = 1f;

    private Rigidbody rb;
    private CapsuleCollider capsule;

    private float inputX;
    private bool jumpPressed;
    private bool isGrounded;
    private bool isCrouching;

    private float standingHeight;
    private Vector3 standingCenter;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        standingHeight = capsule.height;
        standingCenter = capsule.center;
    }

    void Update()
    {
        inputX = 0f;

        if (Input.GetKey(KeyCode.LeftArrow))
        {
            inputX -= 1f;
        }

        if (Input.GetKey(KeyCode.RightArrow))
        {
            inputX += 1f;
        }

        isGrounded = Physics.Raycast(
            transform.position,
            Vector3.down,
            groundCheckDistance
        );

        if (Input.GetKeyDown(KeyCode.UpArrow) && isGrounded && !isCrouching)
        {
            jumpPressed = true;
        }

        if (Input.GetKey(KeyCode.DownArrow))
        {
            Crouch();
        }
        else
        {
            StandUp();
        }
    }

    void FixedUpdate()
    {
        Vector3 v = rb.linearVelocity;
        v.x = inputX * moveSpeed;

        if (jumpPressed)
        {
            v.y = jumpPower;
            jumpPressed = false;
        }

        rb.linearVelocity = v;
    }

    void Crouch()
    {
        isCrouching = true;

        float heightDifference = standingHeight - crouchingHeight;

        capsule.height = crouchingHeight;
        capsule.center = standingCenter + Vector3.down * (heightDifference / 2f);
    }

    void StandUp()
    {
        isCrouching = false;

        capsule.height = standingHeight;
        capsule.center = standingCenter;
    }
}