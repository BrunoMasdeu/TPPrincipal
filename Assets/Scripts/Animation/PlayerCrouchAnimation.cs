using UnityEngine;

/// <summary>Flexiona las piernas sobre la locomoción, conservando los apoyos animados.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator), typeof(Crouch))]
public sealed class PlayerCrouchAnimation : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float hipDropRatio = 0.85f;
    [SerializeField, Range(0f, 0.3f)] private float hipBackOffset = 0.1f;
    [SerializeField, Range(0f, 25f)] private float torsoLean = 10f;

    private Animator animator;
    private Crouch crouch;
    private NetworkPlayerHealth health;
    private Move movement;
    private Transform hips;
    private Transform spine;
    private Transform leftThigh, leftShin, leftFoot;
    private Transform rightThigh, rightShin, rightFoot;

    public float AppliedTorsoLean { get; private set; }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        crouch = GetComponent<Crouch>();
        health = GetComponent<NetworkPlayerHealth>();
        movement = GetComponent<Move>();
        if (animator.avatar == null || !animator.isHuman)
            return;
        hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        leftThigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        leftShin = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        rightThigh = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        rightShin = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
    }

    private void LateUpdate()
    {
        AppliedTorsoLean = 0f;
        if (hips == null || !animator.enabled || !animator.isInitialized ||
            (health != null && health.LifeState != PlayerLifeState.Alive) ||
            (movement != null && movement.wallrunning))
            return;

        float reduction = crouch.HeightReduction;
        if (reduction < 0.001f)
            return;

        // Se parte de la pose que acaba de evaluar el Animator en este frame.
        // Los pies conservan tanto su posición como su orientación durante cada paso.
        Vector3 leftTarget = leftFoot.position;
        Vector3 rightTarget = rightFoot.position;
        Quaternion leftRotation = leftFoot.rotation;
        Quaternion rightRotation = rightFoot.rotation;
        float legLength = Vector3.Distance(leftThigh.position, leftShin.position) +
            Vector3.Distance(leftShin.position, leftTarget);
        float drop = Mathf.Min(reduction * hipDropRatio * transform.lossyScale.y, legLength * 0.7f);
        float weight = crouch.CrouchAmount;
        hips.position -= transform.up * drop + transform.forward * (hipBackOffset * weight);
        if (spine != null)
        {
            AppliedTorsoLean = torsoLean * weight;
            spine.rotation = Quaternion.AngleAxis(torsoLean * weight, transform.right) * spine.rotation;
        }

        BendLeg(leftThigh, leftShin, leftFoot, leftTarget, leftRotation);
        BendLeg(rightThigh, rightShin, rightFoot, rightTarget, rightRotation);
    }

    private void BendLeg(Transform thigh, Transform shin, Transform foot,
        Vector3 target, Quaternion footRotation)
    {
        Vector3 origin = thigh.position;
        float upperLength = Vector3.Distance(origin, shin.position);
        float lowerLength = Vector3.Distance(shin.position, foot.position);
        Vector3 direction = (target - origin).normalized;
        float distance = Mathf.Clamp(Vector3.Distance(origin, target),
            Mathf.Abs(upperLength - lowerLength) + 0.001f, upperLength + lowerLength - 0.001f);
        Vector3 bend = Vector3.ProjectOnPlane(transform.forward, direction).normalized;
        if (bend.sqrMagnitude < 0.01f)
            bend = Vector3.ProjectOnPlane(transform.up, direction).normalized;
        float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) /
            (2f * distance);
        float outward = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
        Vector3 knee = origin + direction * along + bend * outward;
        thigh.rotation = Quaternion.FromToRotation(shin.position - origin, knee - origin) * thigh.rotation;
        shin.rotation = Quaternion.FromToRotation(foot.position - shin.position, target - shin.position) * shin.rotation;
        foot.rotation = footRotation;
    }
}
