using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public enum Skill
{
    Idle,
    Walk,
    Run,

}

[RequireComponent(typeof(JointDriveController))]
public class PlayerAgent : Agent
{
    public string testLog = "";

    [Header("Body Parts")]
    public ArticulationBody hips; // 루트

    // public Transform head; // 바라보는 방향을 기반으로 보상을 판단하기 위한 변수

    // 말단 부위 (L-hand, R-hand, L-foot, R-foot 순서)
    [SerializeField] private List<Transform> _endEffectorList;

    [SerializeField] private List<ArticulationBody> _childList = new List<ArticulationBody>();
    private Vector3 _initPosition; // 초기 루트 포지션
    private List<float> _masterTargetList = new List<float>();

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

        int currentIndex = 0;
        // 루트가 Movable이라면 6개의 인덱스를 건너뛰기 (위치/회전용)
        if (!hips.immovable) currentIndex += 6;

        _jointDriveController.hips = hips;
        foreach (var body in bodyList)
        {
            if (body == hips) continue;
            _jointDriveController.SetupBodyPart(body, currentIndex);
            _childList.Add(body);

            currentIndex += body.dofCount;
        }

        _masterTargetList = new List<float>(new float[currentIndex]);
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
            _jointDriveController.ApplyReferenceStateInitialization(_currentSkill, Random.value);
        }
        else
        {
            _jointDriveController.ResetAllBodyParts();
            referenceCharacter.InitPose(_currentSkill, Random.value);
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
        testLog = "";

        var continuousActions = actionBuffers.ContinuousActions;
        int actionIndex = -1;

        // 목표 각도 설정
        // List<float> targets = new List<float>();
        // int targetCount = hips.GetDriveTargets(targets);
        // log += $"target count: {targetCount}\n";
        // for (int i = 0; i < targetCount; i++)
        // {
        //     targets[i] = continuousActions[++actionIndex];
        // }
        // hips.SetDriveTargets(targets);
        foreach (var body in _childList)
        {
            switch (body.jointType)
            {
                case ArticulationJointType.FixedJoint:
                    break;
                case ArticulationJointType.PrismaticJoint:
                    Debug.LogError($"[{body.name}] unexpected joint type: PrismaticJoint");
                    break;
                case ArticulationJointType.RevoluteJoint:
                    var lowerLimit = body.xDrive.lowerLimit;
                    var upperLimit = body.xDrive.upperLimit;
                    var actionValue = (continuousActions[++actionIndex] + 1f) * 0.5f;
                    var target = Mathf.Lerp(lowerLimit, upperLimit, actionValue);

                    body.SetDriveTarget(ArticulationDriveAxis.X, target);
                    break;
                case ArticulationJointType.SphericalJoint:
                    var targetX = Mathf.Lerp(body.xDrive.lowerLimit, body.xDrive.upperLimit, (continuousActions[++actionIndex] + 1f) * 0.5f);
                    var targetY = Mathf.Lerp(body.yDrive.lowerLimit, body.yDrive.upperLimit, (continuousActions[++actionIndex] + 1f) * 0.5f);
                    var targetZ = Mathf.Lerp(body.zDrive.lowerLimit, body.zDrive.upperLimit, (continuousActions[++actionIndex] + 1f) * 0.5f);

                    body.SetDriveTarget(ArticulationDriveAxis.X, targetX);
                    body.SetDriveTarget(ArticulationDriveAxis.Y, targetY);
                    body.SetDriveTarget(ArticulationDriveAxis.Z, targetZ);
                    break;
            }
        }

        // 최대 힘 결정
        List<float> forces = new List<float>();
        int forceCount = hips.GetDriveForces(forces);
        testLog += $"force count: {forceCount}\n";
        for (int i = 0; i < forceCount; i++)
        {
            forces[i] = (continuousActions[++actionIndex] + 1f) * 0.5f * _jointDriveController.maxJointForce;
        }

        // Debug.Log($"action count: {actionIndex}");
    }

    private void FixedUpdate()
    {
        UpdateLocalFrame();
        referenceCharacter.Tick(Time.fixedDeltaTime);

        // 보상 지급
        var targetHeadingReward = GetTargetHeadingReward(TargetWalkingSpeed, GetAverageVelocity());
        var imitationReward = GetImitationReward();

        // Debug.Log($"target heading reward: {targetHeadingReward}");
        // Debug.Log($"imitation reward: {imitationReward}");

        float reward = 0.3f * targetHeadingReward + 0.7f * imitationReward;
        AddReward(reward);
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

        // Debug.Log($"pose reward: {poseReward}");
        // Debug.Log($"velocity reward: {velocityReward}");
        // Debug.Log($"end effector reward: {endEffectorreward}");

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

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        // 테스트용
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.wKey.wasPressedThisFrame)
        {
            continuousActions[0] = -1f;
        }
        if (keyboard.sKey.wasPressedThisFrame)
        {
            continuousActions[0] = 1f;
        }
        if (keyboard.aKey.wasPressedThisFrame)
        {
            continuousActions[1] = -1f;
        }
        if (keyboard.dKey.wasPressedThisFrame)
        {
            continuousActions[1] = 1f;
        }
    }
}
