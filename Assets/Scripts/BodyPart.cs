using UnityEngine;

[System.Serializable]
public class BodyPart
{
    [Header("Body Part Info")]
    public ArticulationBody body;
    public JointDriveController jointDriveController;
    public int startIndex; // 전체 관절 데이터를 단일 리스트로 관리할 때, 이 파츠는 몇 번 인덱스부터 시작하는지

    [Header("Ground & Target Contact")]
    public GroundContact groundContact;
    public TargetContact targetContact;

    public void Reset(ArticulationReducedSpace? target = null)
    {
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
                Debug.LogError($"[{body.name}] Unexpected Joint Type: Prismatic Joint");
                break;

            case ArticulationJointType.RevoluteJoint:
                // Debug.Log($"[{body.name}] target: [{target?[0] ?? 0f}]");
                body.SetDriveTarget(ArticulationDriveAxis.X, target?[0] ?? 0f);
                // body.jointPosition = target ?? new ArticulationReducedSpace(0f);
                body.jointPosition = new ArticulationReducedSpace(0f);
                body.jointVelocity = new ArticulationReducedSpace(0f);
                break;

            case ArticulationJointType.SphericalJoint:
                // Debug.Log($"[{body.name}] target: [{target?[0] ?? 0f}, {target?[1] ?? 0f}, {target?[2] ?? 0f}]");
                body.SetDriveTarget(ArticulationDriveAxis.X, target?[0] ?? 0f);
                body.SetDriveTarget(ArticulationDriveAxis.Y, target?[1] ?? 0f);
                body.SetDriveTarget(ArticulationDriveAxis.Z, target?[2] ?? 0f);
                // body.jointPosition = target ?? new ArticulationReducedSpace(0f, 0f, 0f);
                body.jointPosition = new ArticulationReducedSpace(0f, 0f, 0f);
                body.jointVelocity = new ArticulationReducedSpace(0f, 0f, 0f);
                break;
        }
    }
}
