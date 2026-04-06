using Unity.Collections;
using UnityEngine;

public class ReferenceBodyPart : MonoBehaviour
{
    [HideInInspector][ReadOnly] public Vector3 initPosition;
    [HideInInspector][ReadOnly] public Quaternion initRotation;

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

    void FixedUpdate()
    {
        UpdateVelocities(Time.fixedDeltaTime);
    }
}
