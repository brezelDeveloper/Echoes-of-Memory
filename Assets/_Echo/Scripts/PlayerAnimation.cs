using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private Animator anim;
    [SerializeField] private Transform model;

    [Header("방향 전환")]
    [SerializeField] private float turnSpeed = 15f;

    [Header("점프 모션")]
    [SerializeField] private string jumpStateName = "Jump";
    [Tooltip("점프 모션을 몇 초 지점부터 재생할지 (앞쪽 웅크림 건너뛰기)")]
    [SerializeField, Range(0f, 1f)] private float jumpClipStart = 0f;

    private Rigidbody rb;
    private PlayerMove move;
    private float targetY = 90f;
    private bool wasJumping;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        move = GetComponent<PlayerMove>();
    }

    void Update()
    {
        Vector3 v = rb.linearVelocity;
        float speed = new Vector2(v.x, v.z).magnitude;
        bool jumping = move.IsJumping;

        if (jumping && !wasJumping)
        {
            anim.CrossFadeInFixedTime(jumpStateName, 0.05f, 0, jumpClipStart);
        }

        wasJumping = jumping;

        anim.SetFloat("Speed", speed);
        anim.SetFloat("WalkRate", Mathf.Max(1f, speed / move.WalkSpeed));
        anim.SetBool("IsGrounded", move.IsGrounded && !jumping);
        anim.SetBool("IsCrouching", move.IsCrouching);
        anim.SetBool("IsRunning", move.IsRunning);

        if (speed > 0.1f)
        {
            targetY = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
        }

        Quaternion target = Quaternion.Euler(0f, targetY, 0f);
        model.localRotation = Quaternion.Slerp(model.localRotation, target, turnSpeed * Time.deltaTime);
    }
}