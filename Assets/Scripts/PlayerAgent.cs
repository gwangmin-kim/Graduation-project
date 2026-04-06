using System;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(JointDriveController))]
public class PlayerAgent : Agent
{
    [Header("Body Parts")]
    [SerializeField] private Transform _hips;
    [SerializeField] private Transform _head;
    // 전체 신체 부위
    [SerializeField] private List<Transform> _childTransformList = new List<Transform>();
    // 말단 부위 (L-hand, R-hand, L-foot, R-foot 순서)
    [SerializeField] private List<Transform> _endEffectorList = new List<Transform>();

    private LocalFrameController _localFrameController;
    private JointDriveController _jointDriveController;
    private EnvironmentParameters _resetParams;

    [Header("Motion Imitation")]
    [SerializeField] private ReferenceCharacterController _referenceCharacter;
    [SerializeField] private bool _isRSIEnabled = false;
    [SerializeField] private Skill _currentSkill = Skill.Walk;

    [Header("Walk")]
    [SerializeField][Range(_minWalkingSpeed, _maxWalkingSpeed)] private float _targetWalkingSpeed = _maxWalkingSpeed;
    [SerializeField] private Vector3 _targetDirection = Vector3.forward;
    [SerializeField] private bool _randomizeWalkSpeedEachEpisode;
    [SerializeField] private bool _randomizeInitialRotation;
    [SerializeField] private float _maxWalkEpisodeTime;

    private const float _minWalkingSpeed = 0.1f;
    private const float _maxWalkingSpeed = 10f;
    public float TargetWalkingSpeed
    {
        get { return _targetWalkingSpeed; }
        set { _targetWalkingSpeed = Mathf.Clamp(value, _minWalkingSpeed, _maxWalkingSpeed); }
    }
    public Vector3 TargetDirection
    {
        get { return _targetDirection; }
        set { value.y = 0f; _targetDirection = value.normalized; }
    }

    private float _timer = 0f;

    [Header("Debug Info")]
    [TextArea(10, 10)][SerializeField] private string _debugLog;

    public override void Initialize()
    {
        _jointDriveController = GetComponent<JointDriveController>();
        _localFrameController = GetComponentInChildren<LocalFrameController>();

        var childList = _hips.GetComponentsInChildren<Rigidbody>();
        foreach (var rigidbody in childList)
        {
            var body = rigidbody.transform;
            _childTransformList.Add(body);
            _jointDriveController.SetupBodyPart(body);
        }
        _jointDriveController.hips = _hips;

        _resetParams = Academy.Instance.EnvironmentParameters;
    }

    public override void OnEpisodeBegin()
    {
        _timer = _maxWalkEpisodeTime;

        if (_isRSIEnabled)
        {
            _jointDriveController.Reset();
            _referenceCharacter.InitPose(_currentSkill, Random.value);
        }
        else
        {
            _jointDriveController.RandomSampleInitialize(_referenceCharacter, _currentSkill);
        }

        _hips.rotation = _randomizeInitialRotation ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) : Quaternion.identity;
        TargetWalkingSpeed = _randomizeInitialRotation ? Random.Range(_minWalkingSpeed, _maxWalkingSpeed) : TargetWalkingSpeed;

        UpdateLocalFrame();
    }

    public void CollectObservationBodyPart(BodyPart bodyPart, VectorSensor sensor)
    {
        // 지면 접촉 여부
        sensor.AddObservation(bodyPart.groundContact.isTouchingGround);

        // 선속도, 각속도
        sensor.AddObservation(_localFrameController.transform.InverseTransformDirection(bodyPart.rigidbody.linearVelocity));
        sensor.AddObservation(_localFrameController.transform.InverseTransformDirection(bodyPart.rigidbody.angularVelocity));

        // 상대위치
        sensor.AddObservation(_localFrameController.transform.InverseTransformDirection(bodyPart.rigidbody.position - _hips.position));

        // 관절 정보 (방향, 힘)
        if (bodyPart.rigidbody.transform != _hips)
        {
            sensor.AddObservation(bodyPart.rigidbody.transform.localRotation);
            sensor.AddObservation(bodyPart.currentStrength / _jointDriveController.maxJointForce);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // 목표 정보
        sensor.AddObservation(_referenceCharacter.CurrentPhase);

        var currentVelocity = GetAverageVelocity();
        var targetVelocity = TargetDirection * TargetWalkingSpeed;

        sensor.AddObservation(Vector3.Distance(targetVelocity, currentVelocity));
        sensor.AddObservation(_localFrameController.transform.InverseTransformDirection(currentVelocity));
        sensor.AddObservation(_localFrameController.transform.InverseTransformDirection(targetVelocity));

        // 전체 회전 상태
        var localForward = _localFrameController.transform.forward;

        sensor.AddObservation(Quaternion.FromToRotation(_hips.forward, localForward));
        sensor.AddObservation(Quaternion.FromToRotation(_head.forward, localForward));

        // 각 관절 상태
        foreach (var bodyPart in _jointDriveController.bodyPartList)
        {
            CollectObservationBodyPart(bodyPart, sensor);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        var continuousActions = actions.ContinuousActions;
        int actionIndex = -1;

        // 관절 각도 설정
        foreach (var bodyPart in _jointDriveController.bodyPartList)
        {
            if (bodyPart.rigidbody.transform == _hips) continue;
            switch (bodyPart.dofCount)
            {
                case 0:
                    break;
                case 1:
                    bodyPart.SetJointTargetRotation(continuousActions[++actionIndex], 0f, 0f);
                    break;
                case 2:
                    // 이 경우 반드시 x축과 y축 회전만 존재하도록 Configurable Joint의 축을 설정해야 함
                    // 현재 휴머노이드 모델엔 해당 관절은 존재하지 않음
                    bodyPart.SetJointTargetRotation(continuousActions[++actionIndex], continuousActions[++actionIndex], 0f);
                    break;
                case 3:
                    bodyPart.SetJointTargetRotation(continuousActions[++actionIndex], continuousActions[++actionIndex], continuousActions[++actionIndex]);
                    break;
                default:
                    Debug.LogError($"Unexpected Dof: {bodyPart.dofCount}");
                    break;
            }
        }

        // 관절 힘 설정
        foreach (var bodyPart in _jointDriveController.bodyPartList)
        {
            if (bodyPart.rigidbody.transform == _hips) continue;
            bodyPart.SetJointStrength(continuousActions[++actionIndex]);
        }
        // Debug.Log(actionIndex);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // base.Heuristic(actionsOut);
    }

    private void UpdateLocalFrame()
    {
        _localFrameController.UpdateOrientation(_hips, TargetDirection);
    }

    private Vector3 GetAverageVelocity()
    {
        Vector3 totalVelocity = Vector3.zero;
        int count = 0;

        foreach (var bodyPart in _jointDriveController.bodyPartList)
        {
            count++;
            totalVelocity += bodyPart.rigidbody.linearVelocity;
        }

        Vector3 averageVelocity = totalVelocity / count;
        return averageVelocity;
    }

    public float GetImitationReward(float poseWeight = 0.7f, float velocityWeight = 0.1f, float endEffectorWeight = 0.2f)
    {
        if (_jointDriveController.bodyPartList.Count != _referenceCharacter.bodyPartList.Count)
        {
            Debug.LogError($"Referenece Character has different number of body parts.");
        }

        float poseReward = GetPoseReward();
        float velocityReward = GetVelocityReward();
        float endEffectorreward = GetEndEffectorReward();

        // Debug.Log($"pose reward: {poseReward}");
        // Debug.Log($"velocity reward: {velocityReward}");
        // Debug.Log($"end effector reward: {endEffectorreward}");

        return poseWeight * poseReward + velocityWeight * velocityReward + endEffectorWeight * endEffectorreward;
    }

    private float GetPoseReward(float weight = -2f)
    {
        float diffSquaredSum = 0f;
        for (int i = 0; i < _jointDriveController.bodyPartList.Count; i++)
        {
            var bodyPart = _jointDriveController.bodyPartList[i];
            var refbodyPart = _referenceCharacter.bodyPartList[i];

            // Pose Error
            var orientation = bodyPart.rigidbody.transform.localRotation;
            var refOrientation = refbodyPart.transform.localRotation;
            float diff = Quaternion.Angle(orientation, refOrientation) * Mathf.Deg2Rad;

            diffSquaredSum += diff * diff;
        }
        return Mathf.Exp(weight * diffSquaredSum);
    }

    private float GetVelocityReward(float weight = -0.1f)
    {
        float diffSquaredSum = 0f;
        for (int i = 0; i < _jointDriveController.bodyPartList.Count; i++)
        {
            var bodyPart = _jointDriveController.bodyPartList[i];
            var refbodyPart = _referenceCharacter.bodyPartList[i];

            // Pose Error
            var velocity = bodyPart.rigidbody.angularVelocity;
            var refVelocity = refbodyPart.AngularVelocity;

            diffSquaredSum += Vector3.SqrMagnitude(velocity - refVelocity);
        }
        return Mathf.Exp(weight * diffSquaredSum);
    }

    private float GetEndEffectorReward()
    {
        if (_endEffectorList.Count != _referenceCharacter.endEffectorList.Count)
        {
            Debug.LogError($"Referenece Character has different number of end-effectors.");
            return 1f;
        }

        float diffSquaredSum = 0f;
        for (int i = 0; i < _endEffectorList.Count; i++)
        {
            var endEffector = _endEffectorList[i];
            var refEndEffector = _referenceCharacter.endEffectorList[i];

            // Pose Error
            var position = endEffector.position - _hips.position;
            var refPosition = refEndEffector.position - _referenceCharacter.hips.position;

            diffSquaredSum += Vector3.SqrMagnitude(position - refPosition);
        }
        return Mathf.Exp(-40f * diffSquaredSum);
    }

    public float GetTargetHeadingReward(float targetSpeed, Vector3 targetDirection, Vector3 actualVelocity, float weight = 2.5f)
    {
        var diff = targetSpeed - Vector3.Dot(actualVelocity, TargetDirection);
        var matchSpeedReward = Mathf.Exp(-weight * diff * diff);

        var headForward = _head.forward;
        headForward.y = 0;
        var lookAtTargetReward = (Vector3.Dot(TargetDirection, headForward) + 1) * 0.5f;

        return matchSpeedReward * lookAtTargetReward;
    }

    private void FixedUpdate()
    {
        UpdateLocalFrame();
        _referenceCharacter.Tick(Time.fixedDeltaTime);

        // 보상 지급
        var imitationReward = GetImitationReward();
        var targetHeadingReward = GetTargetHeadingReward(TargetWalkingSpeed, TargetDirection, GetAverageVelocity());

        // Check for NaNs
        if (float.IsNaN(targetHeadingReward))
        {
            throw new ArgumentException(
                "NaN in targetHeadingReward.\n" +
                $" targetd Speed/Direction: {TargetWalkingSpeed} / {TargetDirection}\n" +
                $" hips.velocity: {_jointDriveController.bodyPartDict[_hips].rigidbody.linearVelocity}\n" +
                $" maximum walking speed: {_maxWalkingSpeed}"
            );
        }

        var reward = 0.7f * imitationReward + 0.3f * targetHeadingReward;
        AddReward(reward);

#if UNITY_EDITOR
        _debugLog = "";
        _debugLog += $"imitation reward: {imitationReward}\n";
        _debugLog += $"task reward: {targetHeadingReward}\n";
        _debugLog += $"total reward: {reward}\n";
#endif

        _timer -= Time.fixedDeltaTime;
        if (_timer < 0f)
        {
            EndEpisode();
        }
    }
}
