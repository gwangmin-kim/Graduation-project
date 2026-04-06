using System.Collections.Generic;
using Unity.MLAgents;
using Unity.VisualScripting;
using UnityEngine;

// [RequireComponent(typeof(Agent))]
public class JointDriveController : MonoBehaviour
{
    [Header("Joint Drive Settings")]
    public float jointSpring;
    public float jointDamper;
    public float maxJointForce;

    public Transform hips;
    public List<BodyPart> bodyPartList = new List<BodyPart>();
    [HideInInspector] public Dictionary<Transform, BodyPart> bodyPartDict = new Dictionary<Transform, BodyPart>();

    private const float _maxAngularVelocity = 100.0f;

    public void Reset()
    {
        foreach (var bodyPart in bodyPartList)
        {
            bodyPart.Reset(bodyPart);
        }
    }

    public void RandomSampleInitialize(ReferenceCharacterController reference, Skill skill)
    {
        if (bodyPartList.Count != reference.bodyPartList.Count)
        {
            Debug.LogError($"Referenece Character has different number of body parts.");
            Reset();
            return;
        }

        reference.InitPose(skill, Random.value);

        CopyReferencePose(reference);
    }

    private void CopyReferencePose(ReferenceCharacterController reference)
    {

        for (int i = 0; i < bodyPartList.Count; i++)
        {
            var bodyPart = bodyPartList[i];
            if (bodyPart.rigidbody.transform == hips) continue;

            bodyPart.Reset(bodyPart);

            var refBodyPart = reference.bodyPartList[i];
            var targetRotation = refBodyPart.JointOrientation;

            bodyPart.joint.targetRotation = targetRotation;
        }
    }

    public void SetupBodyPart(Transform transform)
    {
        var bodyPart = new BodyPart
        {
            initPosition = transform.position,
            initRotation = transform.rotation,

            controller = this,

            rigidbody = transform.GetComponent<Rigidbody>(),
            joint = transform.GetComponent<ConfigurableJoint>(),

            groundContact = transform.GetComponent<GroundContact>(),
        };

        bodyPart.rigidbody.maxAngularVelocity = _maxAngularVelocity;
        if (!bodyPart.groundContact) bodyPart.groundContact = transform.AddComponent<GroundContact>();
        bodyPart.groundContact.agent = GetComponent<Agent>();

        if (bodyPart.joint)
        {
            var jointDrive = new JointDrive
            {
                positionSpring = jointSpring,
                positionDamper = jointDamper,
                maximumForce = maxJointForce,
            };

            bodyPart.joint.slerpDrive = jointDrive;

            int dofCount = 3;
            if (bodyPart.joint.angularXMotion == ConfigurableJointMotion.Locked) dofCount--;
            if (bodyPart.joint.angularYMotion == ConfigurableJointMotion.Locked) dofCount--;
            if (bodyPart.joint.angularZMotion == ConfigurableJointMotion.Locked) dofCount--;

            bodyPart.dofCount = dofCount;
        }

        bodyPartList.Add(bodyPart);
        bodyPartDict.Add(transform, bodyPart);
    }

    public void GetCurrentJointForces()
    {
        foreach (var bodyPart in bodyPartList)
        {
            if (bodyPart.joint)
            {
                bodyPart.currentJointForce = bodyPart.joint.currentForce;
                bodyPart.currentJointForceSqrMag = bodyPart.joint.currentForce.magnitude;
                bodyPart.currentJointTorque = bodyPart.joint.currentTorque;
                bodyPart.currentJointTorqueSqrMag = bodyPart.joint.currentTorque.magnitude;
                if (Application.isEditor)
                {
                    if (bodyPart.jointForceCurve.length > 1000)
                    {
                        bodyPart.jointForceCurve = new AnimationCurve();
                    }

                    if (bodyPart.jointTorqueCurve.length > 1000)
                    {
                        bodyPart.jointTorqueCurve = new AnimationCurve();
                    }

                    bodyPart.jointForceCurve.AddKey(Time.time, bodyPart.currentJointForceSqrMag);
                    bodyPart.jointTorqueCurve.AddKey(Time.time, bodyPart.currentJointTorqueSqrMag);
                }
            }
        }
    }

    // 테스트용
    private void Start()
    {
        if (TryGetComponent<PlayerAgent>(out var playerAgent) && playerAgent.enabled) return;

        Debug.Log("Test");

        if (hips == null) hips = transform.Find("Hips");
        var childList = hips.GetComponentsInChildren<Rigidbody>();
        foreach (var rigidbody in childList)
        {
            var body = rigidbody.transform;
            SetupBodyPart(body);
        }

        var reference = FindAnyObjectByType<ReferenceCharacterController>();
        RandomSampleInitialize(reference, Skill.Walk);
    }
}
