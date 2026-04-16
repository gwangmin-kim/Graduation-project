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

#if UNITY_EDITOR
    [Header("Debug Info")]
    [TextArea(10, 10)][SerializeField] private string _debugLog = "";
    [SerializeField] private Vector3 _targetRotation = Vector3.zero;
#endif

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

    /// ! 현재 문제 많음...
    /// TODO: angle이 항상 양수로 출력됨, ToAngleAxis에서 Axis 방향과 축 방향을 비교해서 부호를 결정해주어야 함
    /// Spherical joint에서 각도 추출하는 로직 자체가 오류가 너무 심함
    public Quaternion GetTargetRotation()
    {
        switch (_jointType)
        {
            case ArticulationJointType.FixedJoint:
            case ArticulationJointType.PrismaticJoint:
                return Quaternion.identity;

            case ArticulationJointType.RevoluteJoint:
                JointOrientation.ToAngleAxis(out float angle, out Vector3 axis);
                // if (Vector3.Dot(axis, transform.right) < 0) angle *= -1f;
#if UNITY_EDITOR
                _targetRotation.x = angle;
                _targetRotation.y = 0f;
                _targetRotation.z = 0f;
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
                _targetRotation.x = x;
                _targetRotation.y = y;
                _targetRotation.z = z;
#endif

                return Quaternion.Euler(x, y, z);
        }

        return Quaternion.identity;
    }

    void FixedUpdate()
    {
        UpdateVelocities(Time.fixedDeltaTime);

#if UNITY_EDITOR
        GetTargetRotation();
        _debugLog = "";
        _debugLog += $"Linear Velocity: {LinearVelocity}\n";
        _debugLog += $"Angular Velocity: {AngularVelocity}\n";
        _debugLog += $"Joint Orientation: {JointOrientation}\n";
        _debugLog += $"Joint Orientation(in Euler): {JointOrientation.eulerAngles}\n";
#endif
    }
}
