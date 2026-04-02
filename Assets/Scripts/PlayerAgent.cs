using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;

public enum Skill
{
    Idle,
    Walk,
    Run,

}

[RequireComponent(typeof(JointDriveController))]
public class PlayerAgent : Agent
{
    [Header("Body Parts")]
    public ArticulationBody hips; // 루트

    // public Transform head; // 바라보는 방향을 기반으로 보상을 판단하기 위한 변수

    // 말단 부위 (L-hand, R-hand, L-foot, R-foot 순서)
    [SerializeField] private List<Transform> _endEffectorList;

    [SerializeField] private List<ArticulationBody> _childList = new List<ArticulationBody>();
    private Vector3 _initPosition; // 초기 루트 포지션

    [Header("Reference Character")]
    public ReferenceCharacterController referenceCharacter;
    [SerializeField] private bool _isRSIEnabled;

    [Header("Current State")]
    [SerializeField] private Skill _currentSkill = Skill.Walk;

    [Header("Walk")]
    [SerializeField][Range(0.1f, 10f)] private float _targetWalkingSpeed = _maxWalkingSpeed;
    [SerializeField] private Vector3 _targetDirection = Vector3.forward;
    public bool randomizeWalkSpeedEachEpisode;
    public bool randomizeInitialRotation;

    const float _maxWalkingSpeed = 10f;
    public float TargetWalkingSpeed
    {
        get { return _targetWalkingSpeed; }
        set { _targetWalkingSpeed = Mathf.Clamp(value, 0.1f, _maxWalkingSpeed); }
    }
    public Vector3 TargetDirection
    {
        get { return _targetDirection; }
        set { value.y = 0f; _targetDirection = value.normalized; }
    }

    private JointDriveController _jointDriveController;
    private LocalFrameController _localFrameController;

    public override void Initialize()
    {
        _jointDriveController = GetComponent<JointDriveController>();
        _localFrameController = GetComponentInChildren<LocalFrameController>();

        _initPosition = hips.transform.position;

        var bodyList = hips.transform.GetComponentsInChildren<ArticulationBody>();
        foreach (var body in bodyList)
        {
            if (body == hips) continue;
            _jointDriveController.SetupBodyPart(body);
            _childList.Add(body);
        }
    }

    public override void OnEpisodeBegin()
    {
        // 위치 초기화
        Quaternion initRotation = randomizeInitialRotation ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) : Quaternion.identity;
        hips.TeleportRoot(_initPosition, initRotation);
        hips.linearVelocity = Vector3.zero;
        hips.angularVelocity = Vector3.zero;

        // 자세 초기화
        if (_isRSIEnabled)
        {
            float initPhase = Random.value;
            _jointDriveController.ApplyReferenceStateInitialization(_currentSkill, initPhase);
        }
        else
        {
            _jointDriveController.ResetAllBodyParts();
        }

        // 목표 속력 재설정
        TargetWalkingSpeed = randomizeWalkSpeedEachEpisode ? Random.Range(0.1f, _maxWalkingSpeed) : TargetWalkingSpeed;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // state information
        // 위상 정보
        sensor.AddObservation(referenceCharacter.CurrentPhase);

        // 루트 정보
        // sensor.AddObservation(hips.transform.localPosition.y); // 위치 (높이 정보만)
        // // ? 평지 테스트용. 이후 지형에 변화를 준다면, '지면으로부터의 상대 높이'를 넘겨줘야 할 수도
        sensor.AddObservation(_localFrameController.GetLocalRotation(hips.transform.rotation)); // 회전
        sensor.AddObservation(_localFrameController.GetLocalDirection(hips.linearVelocity)); // 선속도
        sensor.AddObservation(_localFrameController.GetLocalDirection(hips.angularVelocity)); // 각속도

        // 관절 정보
        foreach (var body in _childList)
        {
            sensor.AddObservation(_localFrameController.GetLocalPosition(body.transform.position)); // 상대위치
            sensor.AddObservation(_localFrameController.GetLocalRotation(body.transform.rotation)); // 상대회전
            sensor.AddObservation(_localFrameController.GetLocalDirection(body.linearVelocity)); // 선속도
            sensor.AddObservation(_localFrameController.GetLocalDirection(body.angularVelocity)); // 각속도

            // sensor.AddObservation(hips.transform.InverseTransformPoint(body.transform.position)); // 상대위치
            // sensor.AddObservation(body.transform.rotation * Quaternion.Inverse(hips.transform.rotation)); // 상대회전
            // sensor.AddObservation(hips.transform.InverseTransformDirection(body.linearVelocity)); // 선속도
            // sensor.AddObservation(hips.transform.InverseTransformDirection(body.angularVelocity)); // 각속도
        }

        // 목표 정보
        // Walk
        var velocityGoal = TargetWalkingSpeed * TargetDirection;
        var averageVelocity = GetAverageVelocity();

        sensor.AddObservation(Vector3.Distance(velocityGoal, averageVelocity));
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        var continuousActions = actionBuffers.ContinuousActions;
        int actionIndex = 0;

        ArticulationDrive ApplyTarget(ArticulationDrive drive)
        {
            float t = (continuousActions[actionIndex++] + 1f) / 2f;
            float target = Mathf.Lerp(drive.lowerLimit, drive.upperLimit, t);
            drive.target = target;
            return drive;
        }

        foreach (var body in _childList)
        {
            switch (body.jointType)
            {
                case ArticulationJointType.FixedJoint:
                    break;

                case ArticulationJointType.PrismaticJoint:
                    body.xDrive = ApplyTarget(body.xDrive);
                    break;

                case ArticulationJointType.RevoluteJoint:
                    body.xDrive = ApplyTarget(body.xDrive);
                    break;

                case ArticulationJointType.SphericalJoint:
                    body.xDrive = ApplyTarget(body.xDrive);
                    body.yDrive = ApplyTarget(body.yDrive);
                    body.zDrive = ApplyTarget(body.zDrive);
                    break;
            }
        }

        var targetHeadingReward = GetTargetHeadingReward(TargetWalkingSpeed, GetAverageVelocity());
        var imitationReward = GetImitationReward();

        float reward = 0.3f * targetHeadingReward + 0.7f * imitationReward;
        AddReward(reward);
    }

    private void FixedUpdate()
    {
        UpdateLocalFrame();
    }

    private void UpdateLocalFrame()
    {
        _localFrameController.UpdateOrientation(hips.transform, TargetDirection);
    }

    private Vector3 GetAverageVelocity()
    {
        Vector3 velocitySum = Vector3.zero;
        foreach (var bodyPart in _jointDriveController.bodyPartList)
        {
            velocitySum += bodyPart.body.linearVelocity;
        }

        return velocitySum / _jointDriveController.bodyPartList.Count;
    }

    private float GetTargetHeadingReward(float targetSpeed, Vector3 currentVelocity, float weight = 2.5f)
    {
        var diff = targetSpeed - Vector3.Dot(currentVelocity, TargetDirection);
        var matchSpeedReward = Mathf.Exp(-weight * diff * diff);

        return matchSpeedReward;
    }

    private float GetImitationReward()
    {
        if (_childList.Count != referenceCharacter.childList.Count)
        {
            Debug.LogError($"Referenece Character has different number of body parts.");
        }

        float poseReward = GetPoseReward();
        float velocityReward = GetVelocityReward();
        float endEffectorreward = GetEndEffectorReward();

        return 0.7f * poseReward + 0.1f * velocityReward + 0.2f * endEffectorreward;
    }

    private float GetPoseReward()
    {
        float GetQuaternionDifference(Quaternion q1, Quaternion q2)
        {
            return Quaternion.Angle(q1, q2) * Mathf.Deg2Rad;
        }

        float diffSquaredSum = 0f;
        for (int i = 0; i < _childList.Count; i++)
        {
            var agent = _childList[i];
            var reference = referenceCharacter.childList[i];

            // Pose Error
            var agentPosition = agent.transform.localRotation;
            var referencePosition = reference.transform.localRotation;
            float diff = GetQuaternionDifference(agentPosition, referencePosition);

            diffSquaredSum += diff * diff;
        }
        return Mathf.Exp(-2f * diffSquaredSum);
    }

    private float GetVelocityReward()
    {
        float diffSquaredSum = 0f;
        for (int i = 0; i < _childList.Count; i++)
        {
            var agent = _childList[i];
            var reference = referenceCharacter.childList[i];

            // Pose Error
            var agentVelocity = agent.angularVelocity;
            var referenceVelocity = reference.AngularVelocity;

            diffSquaredSum += Vector3.SqrMagnitude(agentVelocity - referenceVelocity);
        }
        return Mathf.Exp(-0.1f * diffSquaredSum);
    }

    private float GetEndEffectorReward()
    {
        if (_endEffectorList.Count != referenceCharacter.endEffectorList.Count)
        {
            Debug.LogError($"Referenece Character has different number of end-effectors.");
            return 1f;
        }

        float diffSquaredSum = 0f;
        for (int i = 0; i < _endEffectorList.Count; i++)
        {
            var agent = _endEffectorList[i];
            var reference = referenceCharacter.endEffectorList[i];

            // Pose Error
            var agentPosition = agent.position - hips.transform.position;
            var referencePosition = reference.position - referenceCharacter.hips.position;

            diffSquaredSum += Vector3.SqrMagnitude(agentPosition - referencePosition);
        }
        return Mathf.Exp(-40f * diffSquaredSum);
    }
}
