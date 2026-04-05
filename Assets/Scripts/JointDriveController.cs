using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;
using Unity.MLAgents;

[RequireComponent(typeof(Agent))]
public class JointDriveController : MonoBehaviour
{
    [TextArea(10, 10)]
    public string testLog = "";

    [Header("Physics Settings")]
    // 현재는 일괄 적용
    public float maxJointForce = 10000f;
    public float jointStiffness = 500f;
    public float jointDamping = 50f;

    [HideInInspector] public Dictionary<ArticulationBody, BodyPart> bodyPartDict = new Dictionary<ArticulationBody, BodyPart>();
    public ArticulationBody hips;
    public List<BodyPart> bodyPartList = new List<BodyPart>();

    [Header("Reference Character")]
    [SerializeField] private ReferenceCharacterController _referenceCharacter;

    public void SetupBodyPart(ArticulationBody body, int startIndex)
    {
        switch (body.jointType)
        {
            case ArticulationJointType.FixedJoint:
                break;

            case ArticulationJointType.PrismaticJoint:
                Debug.LogError($"[{body.name}] unexpected joint type: PrismaticJoint");
                break;

            case ArticulationJointType.RevoluteJoint:
                body.SetDriveStiffness(ArticulationDriveAxis.X, jointStiffness);
                body.SetDriveDamping(ArticulationDriveAxis.X, jointDamping);
                body.SetDriveForceLimit(ArticulationDriveAxis.X, maxJointForce);
                break;

            case ArticulationJointType.SphericalJoint:
                body.SetDriveStiffness(ArticulationDriveAxis.X, jointStiffness);
                body.SetDriveStiffness(ArticulationDriveAxis.Y, jointStiffness);
                body.SetDriveStiffness(ArticulationDriveAxis.Z, jointStiffness);

                body.SetDriveDamping(ArticulationDriveAxis.X, jointDamping);
                body.SetDriveDamping(ArticulationDriveAxis.Y, jointDamping);
                body.SetDriveDamping(ArticulationDriveAxis.Z, jointDamping);

                body.SetDriveForceLimit(ArticulationDriveAxis.X, maxJointForce);
                body.SetDriveForceLimit(ArticulationDriveAxis.Y, maxJointForce);
                body.SetDriveForceLimit(ArticulationDriveAxis.Z, maxJointForce);
                break;
        }

        var bodyPart = new BodyPart
        {
            body = body,
            startIndex = startIndex,
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

            bodyPart.body.jointPosition = referenceJointPosition;
            bodyPart.Reset(referenceJointPosition);
        }
    }

    // void Start()
    // {
    //     hips = transform.Find("Hips").GetComponent<ArticulationBody>();
    //     var bodyList = hips.GetComponentsInChildren<ArticulationBody>();

    //     int currentIndex = 0;
    //     if (!hips.immovable) currentIndex += 6;
    //     int dof = 0;
    //     foreach (var body in bodyList)
    //     {
    //         if (body == hips) continue;
    //         SetupBodyPart(body, currentIndex);
    //         currentIndex += body.dofCount;
    //         dof += body.dofCount;
    //     }
    //     Debug.Log(dof);

    //     // TestLog();
    // }

    // void FixedUpdate()
    // {
    //     CopyReferencePose();
    // }

    // public void TestRSI()
    // {
    //     ApplyReferenceStateInitialization(Skill.Walk, Random.value);
    // }

    // public void TestLog()
    // {
    //     log = "";

    //     List<int> testIntList = new List<int>();
    //     int count = hips.GetDofStartIndices(testIntList);
    //     log += $"count: {count}, length: {testIntList.Count}\n[";
    //     foreach (int i in testIntList)
    //     {
    //         log += $"{i}, ";
    //     }
    //     log += "]\n";

    //     List<float> testFloatList = new List<float>();
    //     count = hips.GetDriveTargets(testFloatList);
    //     log += $"count: {count}, length: {testFloatList.Count}\n[";
    //     foreach (float i in testFloatList)
    //     {
    //         log += $"{i:F2}, ";
    //     }
    //     log += "]\n";

    //     count = hips.GetDriveForces(testFloatList);
    //     log += $"count: {count}, length: {testFloatList.Count}\n[";
    //     foreach (float i in testFloatList)
    //     {
    //         log += $"{i:F2}, ";
    //     }
    //     log += "]\n";

    //     // Debug.Log(log);
    // }
}
