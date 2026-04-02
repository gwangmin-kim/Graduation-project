using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;
using Unity.MLAgents;

[System.Serializable]
public class BodyPart
{
    [Header("Body Part Info")]
    public ArticulationBody body;
    public JointDriveController jointDriveController;

    [Header("Ground & Target Contact")]
    public GroundContact groundContact;
    public TargetContact targetContact;

    public void Reset(ArticulationReducedSpace? target = null)
    {
        static ArticulationDrive ResetTarget(ArticulationDrive drive, float target = 0f)
        {
            drive.target = target;
            return drive;
        }

        if (body.isRoot)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        switch (body.jointType)
        {
            case ArticulationJointType.FixedJoint:
                body.jointPosition = new ArticulationReducedSpace();
                body.jointVelocity = new ArticulationReducedSpace();
                break;

            case ArticulationJointType.PrismaticJoint:
                body.xDrive = ResetTarget(body.xDrive);
                body.jointPosition = new ArticulationReducedSpace(0f);
                body.jointVelocity = new ArticulationReducedSpace(0f);
                break;

            case ArticulationJointType.RevoluteJoint:
                body.xDrive = ResetTarget(body.xDrive, target?[0] ?? 0f);
                body.jointPosition = new ArticulationReducedSpace(0f);
                body.jointVelocity = new ArticulationReducedSpace(0f);
                break;

            case ArticulationJointType.SphericalJoint:
                body.xDrive = ResetTarget(body.xDrive, target?[0] ?? 0f);
                body.yDrive = ResetTarget(body.yDrive, target?[1] ?? 0f);
                body.zDrive = ResetTarget(body.zDrive, target?[2] ?? 0f);
                body.jointPosition = new ArticulationReducedSpace(0f, 0f, 0f);
                body.jointVelocity = new ArticulationReducedSpace(0f, 0f, 0f);
                break;
        }
    }
}

[RequireComponent(typeof(Agent))]
public class JointDriveController : MonoBehaviour
{
    [Header("Physics Settings")]
    // 현재는 일괄 적용
    public float maxJointForce = 1000f;
    public float jointStiffness = 500f;
    public float jointDamping = 50f;

    [HideInInspector] public Dictionary<ArticulationBody, BodyPart> bodyPartDict = new Dictionary<ArticulationBody, BodyPart>();
    public List<BodyPart> bodyPartList = new List<BodyPart>();

    [Header("Reference Character")]
    [SerializeField] private ReferenceCharacterController _referenceCharacter;

    public void SetupBodyPart(ArticulationBody body)
    {
        ArticulationDrive ConfigureDrive(ArticulationDrive drive)
        {
            drive.stiffness = jointStiffness;
            drive.damping = jointDamping;
            drive.forceLimit = maxJointForce;
            return drive;
        }

        switch (body.jointType)
        {
            case ArticulationJointType.FixedJoint:
                break;

            case ArticulationJointType.PrismaticJoint:
                Debug.LogError($"[{body.name}] unexpected joint type: PrismaticJoint");
                body.xDrive = ConfigureDrive(body.xDrive);
                break;

            case ArticulationJointType.RevoluteJoint:
                body.xDrive = ConfigureDrive(body.xDrive);
                break;

            case ArticulationJointType.SphericalJoint:
                body.xDrive = ConfigureDrive(body.xDrive);
                body.yDrive = ConfigureDrive(body.yDrive);
                body.zDrive = ConfigureDrive(body.zDrive);
                break;
        }

        var bodyPart = new BodyPart
        {
            body = body,
            jointDriveController = this,
            groundContact = body.transform.GetComponent<GroundContact>()
        };

        if (bodyPart.groundContact == null)
        {
            bodyPart.groundContact = body.transform.AddComponent<GroundContact>();
        }
        bodyPart.groundContact.agent = GetComponent<Agent>();

        bodyPartDict.Add(body, bodyPart);
        bodyPartList.Add(bodyPart);
    }

    public void ResetAllBodyParts()
    {
        foreach (var bodyPart in bodyPartList)
        {
            bodyPart.Reset();
        }
    }

    public void ApplyReferenceStateInitialization(Skill skill, float phase)
    {
        if (bodyPartList.Count != _referenceCharacter.childList.Count)
        {
            Debug.LogError($"Referenece Character has different number of body parts.");
            ResetAllBodyParts();
            return;
        }

        _referenceCharacter.InitPose(skill, phase);
        CopyReferencePose();

    }

    private void CopyReferencePose()
    {
        for (int i = 0; i < bodyPartList.Count; i++)
        {
            var bodyPart = bodyPartList[i];
            var referenceBody = _referenceCharacter.childList[i];
            var referenceJointPosition = referenceBody.GetJointPosition();
            Physics.SyncTransforms();

            bodyPart.body.jointPosition = referenceJointPosition;
            bodyPart.Reset(referenceJointPosition);
        }
    }

    // void Start()
    // {
    //     var hips = transform.Find("Hips");
    //     var bodyList = hips.GetComponentsInChildren<ArticulationBody>();
    //     foreach (var body in bodyList)
    //     {
    //         if (body.transform == hips) continue;
    //         SetupBodyPart(body);
    //     }
    // }

    public void TestRSI()
    {
        ApplyReferenceStateInitialization(Skill.Walk, Random.value);
    }

    // void FixedUpdate()
    // {
    //     CopyReferencePose();
    // }
}
