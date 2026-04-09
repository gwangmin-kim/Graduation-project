using Unity.Collections;
using UnityEngine;

public class ReferenceBodyPart : MonoBehaviour
{
    [SerializeField] private ArticulationJointType _jointType;
    [ReadOnly][SerializeField] private Vector3 _initPosition;
    [ReadOnly][SerializeField] private Quaternion _initRotation;
    [ReadOnly][SerializeField] private Vector3 _initUpAxis;
    [ReadOnly][SerializeField] private Vector3 _initFrontAxis;

    // 속도 계산용
    private Vector3 _prevPosition;
    private Quaternion _prevRotation;

    public Vector3 LinearVelocity { get; private set; }
    public Vector3 AngularVelocity { get; private set; }
    public Quaternion JointOrientation => transform.localRotation * Quaternion.Inverse(_initRotation);

    [Header("Debug Info")]
    [TextArea(10, 10)][SerializeField] private string _debugLog = "";
    [SerializeField] private Vector3 _targetRotation;

    [ContextMenu("Record Initial State")]
    private void RecordInitialState()
    {
        _initPosition = transform.localPosition;
        _initRotation = transform.localRotation;

        _initUpAxis = transform.parent.InverseTransformDirection(transform.up);
        _initFrontAxis = transform.parent.InverseTransformDirection(transform.forward);

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[{name}] Initial State Recorded");
#endif
    }

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
        transform.SetLocalPositionAndRotation(_initPosition, _initRotation);
        ResetVelocities();
    }

    public void ResetVelocities()
    {
        _prevPosition = transform.position;
        _prevRotation = transform.rotation;
        LinearVelocity = Vector3.zero;
        AngularVelocity = Vector3.zero;
    }

    public Quaternion GetTargetRotation()
    {
        switch (_jointType)
        {
            case ArticulationJointType.FixedJoint:
            case ArticulationJointType.PrismaticJoint:
                return Quaternion.identity;

            case ArticulationJointType.RevoluteJoint:
                JointOrientation.ToAngleAxis(out float angle, out Vector3 _);
#if UNITY_EDITOR
                _targetRotation = new Vector3(angle, 0f, 0f);
#endif
                return Quaternion.Euler(angle, 0f, 0f);

            case ArticulationJointType.SphericalJoint:
                var upAxis = transform.parent.InverseTransformDirection(transform.up);
                var swing = Quaternion.FromToRotation(_initUpAxis, upAxis);
                var twist = Quaternion.Inverse(swing) * JointOrientation;
                twist.ToAngleAxis(out float z, out Vector3 _);

                var frontAxis = transform.parent.InverseTransformDirection(transform.forward);
                var swingX = Quaternion.FromToRotation(_initFrontAxis, frontAxis) * Quaternion.Inverse(twist);
                swingX.ToAngleAxis(out float x, out Vector3 _);

                var swingY = swing * Quaternion.Inverse(swingX);
                swingY.ToAngleAxis(out float y, out Vector3 _);

#if UNITY_EDITOR
                _targetRotation = new Vector3(x, y, z);
#endif

                return Quaternion.Euler(x, y, z);
        }

        return Quaternion.identity;
    }

    void FixedUpdate()
    {
        UpdateVelocities(Time.fixedDeltaTime);

#if UNITY_EDITOR
        _debugLog = "";
        _debugLog += $"Linear Velocity: {LinearVelocity}\n";
        _debugLog += $"Angular Velocity: {AngularVelocity}\n";
        _debugLog += $"Joint Orientation: {JointOrientation}\n";
        _debugLog += $"Joint Orientation(in Euler): {JointOrientation.eulerAngles}\n";
#endif
    }
}
