using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [Header("조작 키")]
    [SerializeField] private KeyCode leftKey = KeyCode.A;
    [SerializeField] private KeyCode rightKey = KeyCode.D;
    [Tooltip("화면 안쪽으로 걷기")]
    [SerializeField] private KeyCode forwardKey = KeyCode.W;
    [Tooltip("화면 바깥쪽으로 걷기")]
    [SerializeField] private KeyCode backKey = KeyCode.S;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode crouchKey = KeyCode.LeftControl;
    [SerializeField] private KeyCode grabKey = KeyCode.LeftShift;

    [Header("걷기·달리기")]
    [Tooltip("걷는 속도 (키를 누른 순간의 속도)")]
    [SerializeField] private float moveSpeed = 5f;
    [Tooltip("달리기 속도 (계속 누르면 이 속도까지 서서히 빨라짐)")]
    [SerializeField] private float runSpeed = 8f;
    [Tooltip("키를 계속 누르고 있으면 달리기로 바뀌기까지 걸리는 시간(초)")]
    [SerializeField, Range(0.3f, 5f)] private float timeToRun = 1f;
    [SerializeField] private float crouchSpeed = 2.5f;

    [Header("점프")]
    [SerializeField] private float jumpPower = 5f;
    [Tooltip("키를 누른 뒤 몸이 뜨기까지 걸리는 시간(초)")]
    [SerializeField, Range(0f, 0.6f)] private float jumpWindup = 0.2f;

    [Header("공중 관성")]
    [Tooltip("공중에서 방향키를 누를 때 속도가 바뀌는 정도")]
    [SerializeField] private float airAcceleration = 8f;
    [Tooltip("공중에서 키를 뗐을 때 감속되는 정도")]
    [SerializeField] private float airDeceleration = 1f;

    [Header("판정")]
    [SerializeField] private float groundCheckDistance = 1.1f;
    [SerializeField] private float crouchingHeight = 1f;

    private const float LaunchGraceTime = 0.3f;

    private Rigidbody rb;
    private CapsuleCollider capsule;

    private Vector2 input;
    private Vector2 lastInput;
    private float holdTime;
    private bool isRunning;
    private bool isGrabbing;

    private bool jumpPressed;
    private bool isGrounded;
    private bool isCrouching;

    private bool isJumping;
    private bool isCharging;
    private bool hasLeftGround;
    private float jumpTimer;

    private float standingHeight;
    private Vector3 standingCenter;

    public bool IsGrounded => isGrounded;
    public bool IsCrouching => isCrouching;
    public bool IsJumping => isJumping;
    public bool IsRunning => isRunning;
    public bool IsGrabbing => isGrabbing;
    public float WalkSpeed => moveSpeed;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        standingHeight = capsule.height;
        standingCenter = capsule.center;
    }

    void Update()
    {
        input = Vector2.zero;

        if (Input.GetKey(leftKey)) input.x -= 1f;
        if (Input.GetKey(rightKey)) input.x += 1f;
        if (Input.GetKey(forwardKey)) input.y += 1f;
        if (Input.GetKey(backKey)) input.y -= 1f;

        input = Vector2.ClampMagnitude(input, 1f);

        isGrabbing = Input.GetKey(grabKey);

        isGrounded = Physics.Raycast(
            transform.position,
            Vector3.down,
            groundCheckDistance,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        UpdateRun();

        if (Input.GetKeyDown(jumpKey) && isGrounded && !isCrouching && !isJumping)
        {
            isJumping = true;
            isCharging = true;
            hasLeftGround = false;
            jumpTimer = jumpWindup;
        }

        if (isJumping)
        {
            UpdateJump();
        }

        if (Input.GetKey(crouchKey) && (isGrounded || isCrouching) && !isJumping)
        {
            Crouch();
        }
        else if (isCrouching && CanStandUp())
        {
            StandUp();
        }
    }

    void UpdateRun()
    {
        Vector3 v = rb.linearVelocity;
        float flatSpeed = new Vector2(v.x, v.z).magnitude;

        bool blocked = isGrounded && holdTime > 0.2f && flatSpeed < 0.5f;
        bool turned = Vector2.Dot(input, lastInput) < 0.5f;

        if (input == Vector2.zero || turned || isCrouching || blocked)
        {
            holdTime = 0f;
        }
        else
        {
            holdTime += Time.deltaTime;
        }

        lastInput = input;
        isRunning = holdTime >= timeToRun;
    }

    float CurrentSpeed()
    {
        if (isCrouching)
        {
            return crouchSpeed;
        }

        float t = Mathf.Clamp01(holdTime / timeToRun);
        return Mathf.Lerp(moveSpeed, runSpeed, t);
    }

    void UpdateJump()
    {
        jumpTimer -= Time.deltaTime;

        if (isCharging)
        {
            if (!isGrounded)
            {
                isCharging = false;
                hasLeftGround = true;
            }
            else if (jumpTimer <= 0f)
            {
                isCharging = false;
                jumpPressed = true;
                jumpTimer = LaunchGraceTime;
            }

            return;
        }

        if (!isGrounded)
        {
            hasLeftGround = true;
        }
        else if (hasLeftGround || jumpTimer <= 0f)
        {
            isJumping = false;
        }
    }

    void FixedUpdate()
    {
        Vector3 v = rb.linearVelocity;
        Vector3 target = new Vector3(input.x, 0f, input.y) * CurrentSpeed();

        if (isGrounded)
        {
            v.x = target.x;
            v.z = target.z;
        }
        else
        {
            float rate = input != Vector2.zero ? airAcceleration : airDeceleration;
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            flat = Vector3.MoveTowards(flat, target, rate * Time.fixedDeltaTime);
            v.x = flat.x;
            v.z = flat.z;
        }

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

    bool CanStandUp()
    {
        float checkDistance = standingCenter.y + standingHeight / 2f;

        return !Physics.Raycast(
            transform.position,
            Vector3.up,
            checkDistance,
            ~0,
            QueryTriggerInteraction.Ignore
        );
    }
}