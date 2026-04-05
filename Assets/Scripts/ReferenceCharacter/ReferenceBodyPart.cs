using Unity.Collections;
using UnityEngine;

public class ReferenceBodyPart : MonoBehaviour
{
    [HideInInspector][ReadOnly] public Vector3 initPosition;
    [HideInInspector][ReadOnly] public Quaternion initRotation;
    [ReadOnly] public Vector3 initUpAxis;
    [ReadOnly] public Vector3 initForwardAxis;

    public Transform hips;
    public ArticulationJointType jointType;
    // public Vector3 jointPosition;
    [SerializeField] private Vector3 _jointPosition;
    [TextArea(10, 10)] public string log;

    // 속도 계산용
    private Vector3 _prevPosition;
    private Quaternion _prevRotation;

    public Vector3 LinearVelocity { get; private set; }
    public Vector3 AngularVelocity { get; private set; }
    public Quaternion JointOrientation => transform.localRotation * Quaternion.Inverse(initRotation);

    [ContextMenu("Record Initial State")]
    private void RecordInitialState()
    {
        initPosition = transform.localPosition;
        initRotation = transform.localRotation;
        if (hips == null) hips = transform.root.Find("Hips");
        initUpAxis = hips.InverseTransformDirection(transform.up);
        initForwardAxis = hips.InverseTransformDirection(transform.forward);

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[{name}] Initial State Recorded");
    }

    // private void Awake()
    // {
    //     // initPosition = transform.localPosition;
    //     // initRotation = transform.localRotation;
    //     if (hips == null) hips = transform.root.Find("Hips");
    //     initUpAxis = hips.InverseTransformDirection(transform.up);
    // }

    public void UpdateVelocities(float deltaTime)
    {
        if (deltaTime <= 0f) return;

        LinearVelocity = (transform.position - _prevPosition) / deltaTime;

        Quaternion deltaRotation = transform.rotation * Quaternion.Inverse(_prevRotation);
        deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        AngularVelocity = axis * (angle * Mathf.Deg2Rad) / deltaTime;

        _prevPosition = transform.position;
        _prevRotation = transform.rotation;
    }

    public void ResetToInitialPosition()
    {
        transform.SetLocalPositionAndRotation(initPosition, initRotation);
        ResetVelocities();
    }

    public void ResetVelocities()
    {
        _prevPosition = transform.position;
        _prevRotation = transform.rotation;
        LinearVelocity = Vector3.zero;
        AngularVelocity = Vector3.zero;
    }

    public void UpdateJointPosition()
    {
        var relativeRotation = transform.localRotation * Quaternion.Inverse(initRotation);

        switch (jointType)
        {
            case ArticulationJointType.FixedJoint:
                break;

            case ArticulationJointType.PrismaticJoint:
                Debug.LogError($"[{name}] unexpected joint type: PrismaticJoint");
                break;

            case ArticulationJointType.RevoluteJoint:
                // x축 회전 고정
                float angle = 2.0f * Mathf.Atan2(relativeRotation.x, relativeRotation.w);
                _jointPosition = new Vector3(angle * Mathf.Rad2Deg, 0f, 0f);
                break;

            case ArticulationJointType.SphericalJoint:
                // Quaternion twist =
                //     new Quaternion(0f, transform.localRotation.y, 0f, transform.localRotation.w).normalized
                //     * Quaternion.Inverse(new Quaternion(0f, initRotation.y, 0f, initRotation.w).normalized);
                // Quaternion twist =
                //     new Quaternion(0f, transform.localRotation.y, 0f, transform.localRotation.w).normalized;
                // Quaternion swing = Quaternion.Inverse(twist) * relativeRotation;
                var upAxis = hips.InverseTransformDirection(transform.up);
                var swing = Quaternion.FromToRotation(initUpAxis, upAxis);
                var twist = Quaternion.Inverse(swing) * relativeRotation;

                // twist.ToAngleAxis(out var twistX, out var _);

                // float twistX = 2.0f * Mathf.Atan2(twist.y, twist.w);
                // if (twistX > Mathf.PI) twistX -= 2.0f * Mathf.PI;
                var twistX = 0f;

                float swingY = -Mathf.DeltaAngle(0, swing.eulerAngles.x) * Mathf.Deg2Rad;
                float swingZ = Mathf.DeltaAngle(0, swing.eulerAngles.z) * Mathf.Deg2Rad;

                _jointPosition = new Vector3(twistX, swingY, swingZ) * Mathf.Rad2Deg;

                log = "";
                log += $"relativeRot (in Quaternion): {relativeRotation}\n";
                log += $"relativeRot (in EulerAngles): {relativeRotation.eulerAngles}\n";
                log += $"twist (in Quaternion): {twist}\n";
                log += $"twist (in EulerAngles): {twist.eulerAngles}\n";
                log += $"swing (in Quaternion): {swing}\n";
                log += $"swing (in EulerAngles): {swing.eulerAngles}\n";
                // log += $"delta twist (DeltaAngle): {Mathf.DeltaAngle(initRotation.eulerAngles.y,)}\n";
                log += $"twistX: {twistX * Mathf.Rad2Deg}\n";
                log += $"swingY: {swingY * Mathf.Rad2Deg}\n";
                log += $"swingZ: {swingZ * Mathf.Rad2Deg}\n";
                break;
        }
    }

    public ArticulationReducedSpace GetJointPosition()
    {
        UpdateJointPosition();

        return jointType switch
        {
            ArticulationJointType.FixedJoint => new ArticulationReducedSpace(),
            ArticulationJointType.PrismaticJoint => new ArticulationReducedSpace(0f),
            ArticulationJointType.RevoluteJoint => new ArticulationReducedSpace(_jointPosition.x),
            ArticulationJointType.SphericalJoint => new ArticulationReducedSpace(_jointPosition.x, _jointPosition.y, _jointPosition.z),
            _ => new ArticulationReducedSpace()
        };
    }

    void Update()
    {
        // UpdateJointPosition();
        // UpdateVelocities(Time.deltaTime);

        // log = "";
        // if (TryGetComponent<ArticulationBody>(out var body))
        // {
        //     var pos = body.jointPosition;
        //     var q = Quaternion.identity;
        //     var jointPosition = Vector3.zero;
        //     switch (body.jointType)
        //     {
        //         case ArticulationJointType.RevoluteJoint:
        //             q = Quaternion.Euler(pos[0] * Mathf.Rad2Deg, 0f, 0f);
        //             jointPosition = new Vector3(pos[0], 0f, 0f) * Mathf.Rad2Deg;
        //             break;
        //         case ArticulationJointType.SphericalJoint:
        //             q = Quaternion.Euler(pos[1] * Mathf.Rad2Deg, pos[0] * Mathf.Rad2Deg, pos[2] * Mathf.Rad2Deg);
        //             jointPosition = new Vector3(pos[0], pos[1], pos[2]) * Mathf.Rad2Deg;
        //             break;
        //     }
        //     log += $"joint : {jointPosition}\n";
        //     log += $"ref   : {_jointPosition}\n";
        //     log += $"\n";
        //     log += $"Quaternion Representation\n";
        //     log += $"joint : {q}\n";
        //     log += $"rot   : {JointOrientation}\n";
        //     log += $"diff  : {Quaternion.Angle(q, JointOrientation)}\n";
        //     log += $"joint : {body.angularVelocity}\n";
        //     log += $"ref   : {AngularVelocity}\n";
        //     log += $"diff  : {Vector3.Distance(body.angularVelocity, AngularVelocity)}\n";
        // }
    }
}
