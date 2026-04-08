using UnityEngine;

[System.Serializable]
public class BodyPart
{
    [Header("Body Part Info")]
    public ConfigurableJoint joint;
    public Rigidbody rigidbody;
    public int dofCount;
    [HideInInspector] public Vector3 initPosition;
    [HideInInspector] public Quaternion initRotation;

    [Header("Contact Checker")]
    public ContectChecker contactChecker;

    [HideInInspector] public JointDriveController controller;

    [Header("Current Joint Settings")]
    public Vector3 currentEularJointRotation;
    public float currentStrength;
    public float currentXNormalizedRot;
    public float currentYNormalizedRot;
    public float currentZNormalizedRot;

    [Header("Debug Info")]
    public Vector3 currentJointForce;

    public float currentJointForceSqrMag;
    public Vector3 currentJointTorque;
    public float currentJointTorqueSqrMag;
    public AnimationCurve jointForceCurve = new AnimationCurve();
    public AnimationCurve jointTorqueCurve = new AnimationCurve();

    /// <summary>
    /// 초기 상태로 되돌림 (글로벌 위치까지)
    /// </summary>
    public void Reset(BodyPart bodyPart)
    {
        bodyPart.rigidbody.transform.SetPositionAndRotation(bodyPart.initPosition, bodyPart.initRotation);
        bodyPart.rigidbody.linearVelocity = Vector3.zero;
        bodyPart.rigidbody.angularVelocity = Vector3.zero;

        if (bodyPart.contactChecker)
        {
            bodyPart.contactChecker.isTouchingGround = false;
        }
    }

    /// <summary>
    /// Apply torque according to defined goal `x, y, z` angle and force `strength`.
    /// </summary>
    public void SetJointTargetRotation(float x, float y, float z)
    {
        x = (x + 1f) * 0.5f;
        y = (y + 1f) * 0.5f;
        z = (z + 1f) * 0.5f;

        var xRot = Mathf.Lerp(joint.lowAngularXLimit.limit, joint.highAngularXLimit.limit, x);
        var yRot = Mathf.Lerp(-joint.angularYLimit.limit, joint.angularYLimit.limit, y);
        var zRot = Mathf.Lerp(-joint.angularZLimit.limit, joint.angularZLimit.limit, z);

        currentXNormalizedRot = Mathf.InverseLerp(joint.lowAngularXLimit.limit, joint.highAngularXLimit.limit, xRot);
        currentYNormalizedRot = Mathf.InverseLerp(-joint.angularYLimit.limit, joint.angularYLimit.limit, yRot);
        currentZNormalizedRot = Mathf.InverseLerp(-joint.angularZLimit.limit, joint.angularZLimit.limit, zRot);

        joint.targetRotation = Quaternion.Euler(xRot, yRot, zRot);
        currentEularJointRotation = new Vector3(xRot, yRot, zRot);
    }

    public void SetJointStrength(float strength)
    {
        var maxForce = (strength + 1f) * 0.5f * controller.maxJointForce;
        var jointDrive = new JointDrive
        {
            positionSpring = controller.jointSpring,
            positionDamper = controller.jointDamper,
            maximumForce = maxForce
        };
        joint.slerpDrive = jointDrive;
        currentStrength = jointDrive.maximumForce;
    }
}
