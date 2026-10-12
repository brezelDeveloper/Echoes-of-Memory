using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [System.Serializable]
    public class BaseView
    {
        public string name = "구간";
        [Tooltip("메아리가 이 상자에 들어오면 이 구도가 새 기본 카메라가 됨")]
        public BoxCollider area;
        [Tooltip("메아리 기준 좌우 치우침 (양수면 오른쪽 앞길이 넓게 보임)")]
        public float offsetX = 0f;
        [Tooltip("카메라가 바라보는 높이 (메아리 몸 중심 기준)")]
        public float lookHeight = 0.5f;
        [Tooltip("메아리에서 카메라까지 거리")]
        public float distance = 10f;
        [Tooltip("X = 내려다보는 각도, Y = 옆으로 돌아보는 각도")]
        public Vector2 angle = new Vector2(10f, 0f);
        [Range(20f, 90f)] public float fieldOfView = 60f;
        [Tooltip("이 구도로 바뀌는 데 걸리는 시간(초)")]
        [Range(0.1f, 5f)] public float transitionTime = 1f;
    }

    [System.Serializable]
    public class Ramp
    {
        public string name = "점진 구간";
        [Tooltip("이 상자의 왼쪽 끝에서 오른쪽 끝으로 갈수록 구도가 점점 바뀜")]
        public BoxCollider area;
        [Tooltip("왼쪽 끝 구도 = 이 이름의 구간 (시작 카메라는 '시작')")]
        public string fromSection = "시작";
        [Tooltip("오른쪽 끝 구도 = 이 이름의 구간")]
        public string toSection = "";
        [System.NonSerialized] public bool warned;
    }

    [System.Serializable]
    public class SpecialShot
    {
        public string name = "특수 장면";
        [Tooltip("메아리가 이 상자 안에 있는 동안만 실행. 비워두면 PlaySpecial로만 실행")]
        public BoxCollider area;
        [Tooltip("카메라가 설 자리 (위치와 각도)")]
        public Transform cameraPoint;
        [Tooltip("지정하면 카메라가 이 대상을 계속 바라봄")]
        public Transform lookAt;
        [Tooltip("켜면 카메라 자리가 메아리를 따라 좌우로 이동")]
        public bool followPlayerX = false;
        [Range(20f, 90f)] public float fieldOfView = 50f;
        [Tooltip("이 장면으로 들어가고 나오는 데 걸리는 시간(초)")]
        [Range(0.1f, 5f)] public float transitionTime = 1f;
    }

    private struct ViewValues
    {
        public float offsetX;
        public float lookHeight;
        public float frame;
        public float angleX;
        public float angleY;
        public float fov;

        public static ViewValues Lerp(ViewValues a, ViewValues b, float t)
        {
            return new ViewValues
            {
                offsetX = Mathf.Lerp(a.offsetX, b.offsetX, t),
                lookHeight = Mathf.Lerp(a.lookHeight, b.lookHeight, t),
                frame = Mathf.Lerp(a.frame, b.frame, t),
                angleX = Mathf.LerpAngle(a.angleX, b.angleX, t),
                angleY = Mathf.LerpAngle(a.angleY, b.angleY, t),
                fov = Mathf.Lerp(a.fov, b.fov, t)
            };
        }
    }

    private struct ViewVelocity
    {
        public float offsetX;
        public float lookHeight;
        public float frame;
        public float angleX;
        public float angleY;
        public float fov;
    }

    [Header("대상")]
    [SerializeField] private Transform target;

    [Header("따라가기")]
    [Tooltip("좌우·높이·깊이를 따라가는 부드러움 (클수록 느긋함)")]
    [SerializeField] private float smoothTime = 0.25f;
    [Tooltip("메아리의 앞뒤(W/S) 이동을 따라가는 정도 (1 = 완전히 따라감, 0 = 시작 깊이에 고정)")]
    [SerializeField, Range(0f, 1f)] private float depthFollowAmount = 1f;

    [Header("화면 이탈 방지")]
    [Tooltip("메아리가 화면 가장자리에서 이 비율보다 안쪽에 머물게 함 (0 = 끄기)")]
    [SerializeField, Range(0f, 0.45f)] private float screenMargin = 0.12f;

    [Header("진단")]
    [Tooltip("Game 화면 왼쪽 위에 현재 구간·램프·카메라 숫자를 표시")]
    [SerializeField] private bool showDebug = true;

    [Header("시작 카메라 (첫 구간에 들어가기 전, Area는 비워두기)")]
    [SerializeField] private BaseView startView = new BaseView { name = "시작" };

    [Header("구간별 기본 카메라 (들어가면 그 구도가 기본이 됨)")]
    [SerializeField] private BaseView[] sections;

    [Header("점진 구간 (걸어가는 만큼 구도가 서서히 바뀜, 구간보다 우선)")]
    [SerializeField] private Ramp[] ramps;

    [Header("특수 장면")]
    [SerializeField] private SpecialShot[] specialShots;

    private Camera cam;
    private PlayerMove move;

    private BaseView currentView;
    private ViewValues current;
    private ViewVelocity velocity;

    private float startZ;
    private float groundY;
    private float followX;
    private float followY;
    private float followZ;
    private float velocityX;
    private float velocityY;
    private float velocityZ;

    private SpecialShot manualShot;
    private float specialWeight;
    private float specialBlend;
    private float exitTime = 1f;
    private Vector3 specialPos;
    private Quaternion specialRot;
    private float specialFov;

    private string debugState = "";
    private GUIStyle debugStyle;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("Camera Follow의 Target이 비어 있어요");
            return;
        }

        Validate();

        move = target.GetComponent<PlayerMove>();
        startZ = target.position.z;
        groundY = target.position.y;
        followX = target.position.x;
        followY = groundY;
        followZ = DepthGoal();

        currentView = FindSection() ?? startView;
        current = GoalValues(out _);
        velocity = default;

        Compose(out Vector3 pos, out Quaternion rot, out float fov);
        Apply(pos, rot, fov);
    }

    void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        BaseCamera(out Vector3 pos, out Quaternion rot, out float fov);
        SpecialCamera(ref pos, ref rot, ref fov);
        Apply(pos, rot, fov);
    }

    void Apply(Vector3 pos, Quaternion rot, float fov)
    {
        transform.SetPositionAndRotation(pos, rot);
        cam.fieldOfView = fov;
        KeepInView();
    }

    void KeepInView()
    {
        if (screenMargin <= 0f)
        {
            return;
        }

        Vector3 point = target.position;
        Vector3 vp = cam.WorldToViewportPoint(point);

        if (vp.z <= cam.nearClipPlane)
        {
            return;
        }

        float x = Mathf.Clamp(vp.x, screenMargin, 1f - screenMargin);
        float y = Mathf.Clamp(vp.y, screenMargin, 1f - screenMargin);

        if (Mathf.Approximately(x, vp.x) && Mathf.Approximately(y, vp.y))
        {
            return;
        }

        Vector3 desired = cam.ViewportToWorldPoint(new Vector3(x, y, vp.z));
        transform.position += (point - desired) * (1f - specialBlend);
    }

    float DepthGoal()
    {
        return Mathf.Lerp(startZ, target.position.z, depthFollowAmount);
    }

    void BaseCamera(out Vector3 pos, out Quaternion rot, out float fov)
    {
        bool grounded = move == null || move.IsGrounded;

        if (grounded || target.position.y < groundY)
        {
            groundY = target.position.y;
        }

        followX = Mathf.SmoothDamp(followX, target.position.x, ref velocityX, smoothTime);
        followY = Mathf.SmoothDamp(followY, groundY, ref velocityY, smoothTime);
        followZ = Mathf.SmoothDamp(followZ, DepthGoal(), ref velocityZ, smoothTime);

        ViewValues goal = GoalValues(out float time);
        Damp(goal, time);
        Compose(out pos, out rot, out fov);
    }

    ViewValues GoalValues(out float time)
    {
        if (TryRamp(out ViewValues goal, out Ramp ramp, out float p))
        {
            time = Mathf.Max(0.01f, smoothTime);
            debugState = "램프 [" + ramp.name + "] " + Mathf.RoundToInt(p * 100f) + "%   왼쪽 끝 = " + ramp.fromSection + " / 오른쪽 끝 = " + ramp.toSection;
            return goal;
        }

        BaseView section = FindSection();

        if (section != null)
        {
            currentView = section;
            debugState = "구간 [" + currentView.name + "]";
        }
        else
        {
            debugState = "상자 밖 → 마지막 구도 유지 [" + currentView.name + "]";
        }

        time = Mathf.Max(0.01f, currentView.transitionTime / 3f);
        return Values(currentView);
    }

    void Damp(ViewValues goal, float time)
    {
        current.offsetX = Mathf.SmoothDamp(current.offsetX, goal.offsetX, ref velocity.offsetX, time);
        current.lookHeight = Mathf.SmoothDamp(current.lookHeight, goal.lookHeight, ref velocity.lookHeight, time);
        current.frame = Mathf.SmoothDamp(current.frame, goal.frame, ref velocity.frame, time);
        current.fov = Mathf.SmoothDamp(current.fov, goal.fov, ref velocity.fov, time);
        current.angleX = Mathf.SmoothDampAngle(current.angleX, goal.angleX, ref velocity.angleX, time);
        current.angleY = Mathf.SmoothDampAngle(current.angleY, goal.angleY, ref velocity.angleY, time);
    }

    float CurrentDistance()
    {
        return current.frame / (2f * Mathf.Tan(current.fov * 0.5f * Mathf.Deg2Rad));
    }

    void Compose(out Vector3 pos, out Quaternion rot, out float fov)
    {
        fov = current.fov;
        rot = Quaternion.Euler(current.angleX, current.angleY, 0f);
        Vector3 pivot = new Vector3(followX + current.offsetX, followY + current.lookHeight, followZ);
        pos = pivot - rot * Vector3.forward * CurrentDistance();
    }

    bool TryRamp(out ViewValues goal, out Ramp ramp, out float p)
    {
        goal = default;
        p = 0f;
        ramp = FindRamp();

        if (ramp == null)
        {
            return false;
        }

        BaseView from = FindView(ramp.fromSection);
        BaseView to = FindView(ramp.toSection);

        if (from == null || to == null)
        {
            if (!ramp.warned)
            {
                Debug.LogWarning("점진 구간 [" + ramp.name + "]의 From/To 이름과 같은 구간이 없어요: " + ramp.fromSection + " / " + ramp.toSection);
                ramp.warned = true;
            }

            return false;
        }

        Bounds b = ramp.area.bounds;
        p = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(b.min.x, b.max.x, target.position.x));

        goal = ViewValues.Lerp(Values(from), Values(to), p);
        currentView = p < 0.5f ? from : to;
        return true;
    }

    void SpecialCamera(ref Vector3 pos, ref Quaternion rot, ref float fov)
    {
        SpecialShot shot = manualShot ?? FindSpecial();

        if (shot != null && shot.cameraPoint != null)
        {
            specialPos = shot.cameraPoint.position;

            if (shot.followPlayerX)
            {
                float offset = shot.area != null ? specialPos.x - shot.area.bounds.center.x : 0f;
                specialPos.x = followX + offset;
            }

            specialRot = shot.lookAt != null
                ? Quaternion.LookRotation(shot.lookAt.position - specialPos)
                : shot.cameraPoint.rotation;

            specialFov = shot.fieldOfView;
            exitTime = shot.transitionTime;
            specialWeight = Mathf.MoveTowards(specialWeight, 1f, Time.deltaTime / shot.transitionTime);
            debugState = "특수 장면 [" + shot.name + "]";
        }
        else
        {
            specialWeight = Mathf.MoveTowards(specialWeight, 0f, Time.deltaTime / exitTime);
        }

        if (specialWeight <= 0f)
        {
            specialBlend = 0f;
            return;
        }

        specialBlend = Mathf.SmoothStep(0f, 1f, specialWeight);
        pos = Vector3.Lerp(pos, specialPos, specialBlend);
        rot = Quaternion.Slerp(rot, specialRot, specialBlend);
        fov = Mathf.Lerp(fov, specialFov, specialBlend);
    }

    public void PlaySpecial(string shotName)
    {
        if (specialShots != null)
        {
            foreach (SpecialShot s in specialShots)
            {
                if (s.name == shotName)
                {
                    manualShot = s;
                    return;
                }
            }
        }

        Debug.LogWarning("특수 장면을 찾을 수 없어요: " + shotName);
    }

    public void StopSpecial()
    {
        manualShot = null;
    }

    void Validate()
    {
        HashSet<string> names = new HashSet<string> { startView.name };

        if (sections != null)
        {
            foreach (BaseView s in sections)
            {
                if (!names.Add(s.name))
                {
                    Debug.LogWarning("카메라 구간 이름이 겹쳐요: [" + s.name + "] — 점진 구간이 엉뚱한 구간을 가리킬 수 있어요");
                }

                if (s.area == null)
                {
                    Debug.LogWarning("카메라 구간 [" + s.name + "]의 Area가 비어 있어요");
                }
            }
        }

        if (ramps == null)
        {
            return;
        }

        foreach (Ramp r in ramps)
        {
            BaseView from = FindView(r.fromSection);
            BaseView to = FindView(r.toSection);

            if (from == null || to == null)
            {
                Debug.LogWarning("점진 구간 [" + r.name + "]의 From/To 이름과 같은 구간이 없어요: " + r.fromSection + " / " + r.toSection);
                continue;
            }

            if (r.area != null && from.area != null && to.area != null &&
                from.area.bounds.center.x > to.area.bounds.center.x)
            {
                Debug.LogWarning("점진 구간 [" + r.name + "]의 From/To가 뒤바뀐 것 같아요 (From이 오른쪽에 있음)");
            }
        }
    }

    ViewValues Values(BaseView view)
    {
        float fov = Mathf.Clamp(view.fieldOfView, 1f, 179f);
        float distance = Mathf.Max(0.1f, view.distance);

        return new ViewValues
        {
            offsetX = view.offsetX,
            lookHeight = view.lookHeight,
            frame = 2f * distance * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad),
            angleX = view.angle.x,
            angleY = view.angle.y,
            fov = fov
        };
    }

    BaseView FindView(string viewName)
    {
        if (startView.name == viewName)
        {
            return startView;
        }

        if (sections != null)
        {
            foreach (BaseView s in sections)
            {
                if (s.name == viewName)
                {
                    return s;
                }
            }
        }

        return null;
    }

    BaseView FindSection()
    {
        if (sections == null)
        {
            return null;
        }

        for (int i = sections.Length - 1; i >= 0; i--)
        {
            BaseView s = sections[i];

            if (s.area != null && s.area.bounds.Contains(target.position))
            {
                return s;
            }
        }

        return null;
    }

    Ramp FindRamp()
    {
        if (ramps == null)
        {
            return null;
        }

        for (int i = ramps.Length - 1; i >= 0; i--)
        {
            Ramp r = ramps[i];

            if (r.area != null && r.area.bounds.Contains(target.position))
            {
                return r;
            }
        }

        return null;
    }

    SpecialShot FindSpecial()
    {
        if (specialShots == null)
        {
            return null;
        }

        for (int i = specialShots.Length - 1; i >= 0; i--)
        {
            SpecialShot s = specialShots[i];

            if (s.area != null && s.area.bounds.Contains(target.position))
            {
                return s;
            }
        }

        return null;
    }

    void OnGUI()
    {
        if (!showDebug || target == null || !Application.isPlaying)
        {
            return;
        }

        if (debugStyle == null)
        {
            debugStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            debugStyle.normal.textColor = Color.yellow;
        }

        string numbers = "거리 " + CurrentDistance().ToString("0.0") +
                         "   FOV " + current.fov.ToString("0") +
                         "   각도 " + current.angleX.ToString("0") + " / " + current.angleY.ToString("0");

        GUI.Label(new Rect(12f, 12f, 1200f, 60f), debugState + "\n" + numbers, debugStyle);
    }

    void OnDrawGizmos()
    {
        if (sections != null)
        {
            foreach (BaseView s in sections)
            {
                if (s.area == null)
                {
                    continue;
                }

                DrawBox(s.area, new Color(0.2f, 0.8f, 1f));

                Vector3 center = s.area.transform.TransformPoint(s.area.center);
                float bottom = center.y - s.area.size.y * s.area.transform.lossyScale.y * 0.5f;
                float y = target != null ? target.position.y : bottom + 1f;
                float z = target != null ? target.position.z : center.z;
                Quaternion r = Quaternion.Euler(s.angle.x, s.angle.y, 0f);
                Vector3 pivot = new Vector3(center.x + s.offsetX, y + s.lookHeight, z);
                DrawCamera(pivot - r * Vector3.forward * Mathf.Max(0.1f, s.distance), r, s.fieldOfView);
            }
        }

        if (ramps != null)
        {
            foreach (Ramp r in ramps)
            {
                if (r.area != null)
                {
                    DrawBox(r.area, new Color(0.4f, 1f, 0.4f));
                }
            }
        }

        if (specialShots != null)
        {
            foreach (SpecialShot s in specialShots)
            {
                if (s.area != null)
                {
                    DrawBox(s.area, new Color(1f, 0.6f, 0.2f));
                }

                if (s.cameraPoint == null)
                {
                    continue;
                }

                Gizmos.color = new Color(1f, 0.6f, 0.2f);
                Quaternion r = s.lookAt != null
                    ? Quaternion.LookRotation(s.lookAt.position - s.cameraPoint.position)
                    : s.cameraPoint.rotation;
                DrawCamera(s.cameraPoint.position, r, s.fieldOfView);
            }
        }

        Gizmos.matrix = Matrix4x4.identity;
    }

    void DrawBox(BoxCollider box, Color color)
    {
        Gizmos.matrix = box.transform.localToWorldMatrix;
        Gizmos.color = new Color(color.r, color.g, color.b, 0.12f);
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = color;
        Gizmos.DrawWireCube(box.center, box.size);
    }

    void DrawCamera(Vector3 pos, Quaternion rot, float fov)
    {
        Gizmos.matrix = Matrix4x4.TRS(pos, rot, Vector3.one);
        Gizmos.DrawFrustum(Vector3.zero, fov, 3f, 0.3f, 16f / 9f);
    }
}