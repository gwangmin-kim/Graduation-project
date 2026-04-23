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
    [SerializeField] private Transform _footL;
    [SerializeField] private Transform _footR;
    [SerializeField] private Transform _handL;
    [SerializeField] private Transform _handR;
    // 전체 신체 부위
    [SerializeField] private List<Transform> _childTransformList = new List<Transform>();
    // 말단 부위 (L-hand, R-hand, L-foot, R-foot 순서)
    [SerializeField] private List<Transform> _endEffectorList = new List<Transform>();

    private LocalFrameController _localFrameController;
    private JointDriveController _jointDriveController;
    private EnvironmentParameters _resetParams;

    [Header("Motion Cloning")]
    [SerializeField] private bool _useReferenceMotion;
    [SerializeField] private ReferenceCharacterController _referenceCharacter;
    [SerializeField] private bool _isRSIEnabled = false;
    [SerializeField] private Skill _currentSkill = Skill.Walk;
    [Range(_minAnimationSpeed, _maxAnimationSpeed)][SerializeField] private float _animationSpeed = 1f;
    private const float _minAnimationSpeed = 0.5f;
    private const float _maxAnimationSpeed = 1.5f;

    [Header("Walk")]
    [SerializeField] private Transform _target;
    [SerializeField] private float _targetTouchReward = 1f;
    [SerializeField][Range(_minWalkingSpeed, _maxWalkingSpeed)] private float _targetWalkingSpeed = _maxWalkingSpeed;
    [SerializeField] private bool _randomizeWalkSpeedEachEpisode;
    [SerializeField] private bool _lookAtTargetEachEpisode;

    private const float _minWalkingSpeed = 0.0f;
    private const float _maxWalkingSpeed = 3.5f;
    private Vector3 _worldDirectionToWalk = Vector3.forward;
    public float TargetWalkingSpeed
    {
        get { return _targetWalkingSpeed; }
        set { _targetWalkingSpeed = Mathf.Clamp(value, _minWalkingSpeed, _maxWalkingSpeed); }
    }

#if UNITY_EDITOR
    [Header("Debug Info")]
    [TextArea(10, 10)][SerializeField] private string _debugLog;
    public bool isTestMode = false;
    public float poseReward;
    public float velocityReward;
    public float endEffectorReward;
#endif

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
        _jointDriveController.bodyPartDict[_hips].isRoot = true;

        _resetParams = Academy.Instance.EnvironmentParameters;
    }

    public override void OnEpisodeBegin()
    {

        if (_useReferenceMotion && _isRSIEnabled)
        {
            _jointDriveController.RandomSampleInitialize(_referenceCharacter, _currentSkill);

            if (_lookAtTargetEachEpisode)
            {
                var forward = _target.position - _hips.position;
                forward.y = 0f;
                forward = forward.normalized;
                _hips.rotation = Quaternion.LookRotation(forward);
            }
            else
            {
                _hips.Rotate(0f, Random.Range(0f, 360f), 0f);
            }
        }
        else
        {
            _jointDriveController.Reset();

            if (_lookAtTargetEachEpisode)
            {
                var forward = _target.position - _hips.position;
                forward.y = 0f;
                forward = forward.normalized;
                _hips.rotation = Quaternion.LookRotation(forward);
            }
            else
            {
                _hips.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }

            if (_useReferenceMotion) _referenceCharacter.InitPose(_currentSkill, Random.value);
        }

        // TargetWalkingSpeed = _randomizeWalkSpeedEachEpisode ? Random.Range(_minWalkingSpeed, _maxWalkingSpeed) : TargetWalkingSpeed;
        if (_randomizeWalkSpeedEachEpisode)
        {
            TargetWalkingSpeed = Random.Range(_minWalkingSpeed, _maxWalkingSpeed);
            // float t = Mathf.InverseLerp(_minWalkingSpeed, _maxWalkingSpeed, TargetWalkingSpeed);
            // _animationSpeed = Mathf.Lerp(_minAnimationSpeed, _maxAnimationSpeed, t);
        }

        UpdateOrientation();
    }

    public void CollectObservationBodyPart(BodyPart bodyPart, VectorSensor sensor)
    {
        // 지면 접촉 여부
        sensor.AddObservation(bodyPart.contactChecker.isTouchingGround);

        // 선속도, 각속도
        sensor.AddObservation(_localFrameController.transform.InverseTransformDirection(bodyPart.rigidbody.linearVelocity));
        sensor.AddObservation(_localFrameController.transform.InverseTransformDirection(bodyPart.rigidbody.angularVelocity));

        // 상대위치
        sensor.AddObservation(_localFrameController.transform.InverseTransformDirection(bodyPart.rigidbody.position - _hips.position));

        // 관절 정보 (방향, 힘)
        if (bodyPart.dofCount > 0)
        {
            sensor.AddObservation(bodyPart.rigidbody.transform.localRotation);
            sensor.AddObservation(bodyPart.currentStrength / _jointDriveController.maxJointForce);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // NaN 검사
        if (float.IsNaN(_hips.position.x) || float.IsNaN(_hips.rotation.x))
        {
            Debug.LogWarning("NaN detected in physics! Resetting agent...");
            EndEpisode();
            return;
        }

        // 목표 관련 정보
        var localForward = _localFrameController.transform.forward;
        var currentVelocity = GetAverageVelocity();
        var targetVelocity = localForward * TargetWalkingSpeed;

        sensor.AddObservation(Vector3.Distance(targetVelocity, currentVelocity));
        sensor.AddObservation(_localFrameController.transform.InverseTransformDirection(currentVelocity));
        sensor.AddObservation(_localFrameController.transform.InverseTransformDirection(targetVelocity));

        sensor.AddObservation(_localFrameController.transform.InverseTransformPoint(_target.transform.position));

        // 몸 정렬 상태
        sensor.AddObservation(Quaternion.FromToRotation(_hips.forward, localForward));
        sensor.AddObservation(Quaternion.FromToRotation(_head.forward, localForward));

        // 각 관절 상태
        foreach (var bodyPart in _jointDriveController.bodyPartList)
        {
            CollectObservationBodyPart(bodyPart, sensor);
        }

        // (모션 모방 시) 위상 정보
        sensor.AddObservation(_referenceCharacter.CurrentPhase);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
#if UNITY_EDITOR
        if (isTestMode) return;
#endif

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
            if (bodyPart.dofCount == 0) continue;
            bodyPart.SetJointStrength(continuousActions[++actionIndex]);
        }

#if UNITY_EDITOR
        Debug.Log($"action count: {actionIndex + 1}");
#endif
    }

    private void UpdateOrientation()
    {
        _worldDirectionToWalk = _target.position - _hips.position;
        _localFrameController.UpdateOrientation(_hips, _worldDirectionToWalk);
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

    private float GetImitationReward(float poseWeight = 0.7f, float velocityWeight = 0.1f, float endEffectorWeight = 0.2f)
    {
        if (_jointDriveController.bodyPartList.Count != _referenceCharacter.bodyPartList.Count)
        {
            Debug.LogError($"Referenece Character has different number of body parts.");
        }

        float poseReward = GetPoseReward();
        float velocityReward = GetVelocityReward();
        float endEffectorReward = GetEndEffectorReward();

#if UNITY_EDITOR
        // Debug.Log($"pose reward: {poseReward}");
        // Debug.Log($"velocity reward: {velocityReward}");
        // Debug.Log($"end effector reward: {endEffectorreward}");
        this.poseReward = poseReward;
        this.velocityReward = velocityReward;
        this.endEffectorReward = endEffectorReward;
#endif

        return poseWeight * poseReward + velocityWeight * velocityReward + endEffectorWeight * endEffectorReward;
    }

    private float GetPoseReward(float weight = -0.8f)
    {
        float diffSquaredSum = 0f;
        for (int i = 0; i < _jointDriveController.bodyPartList.Count; i++)
        {
            var bodyPart = _jointDriveController.bodyPartList[i];
            if (bodyPart.dofCount == 0) continue;

            var refbodyPart = _referenceCharacter.bodyPartList[i];

            // Pose Error
            // // 루트(hips) 기준 상대 회전을 비교
            // var orientation = bodyPart.rigidbody.transform.rotation * Quaternion.Inverse(_hips.rotation);
            // var refOrientation = refbodyPart.transform.rotation * Quaternion.Inverse(_referenceCharacter.hips.rotation);
            // float diff = Quaternion.Angle(orientation, refOrientation) * Mathf.Deg2Rad;

            // diffSquaredSum += diff * diff;

            // 변경: 회전이 아니라, 로컬 축의 방향을 비교
            // 조금 더 느슨한 기준이지만, 현재 모델이 제대로 동작을 재현하지 못하고 있기에 이렇게 시도
            var orientation = _hips.InverseTransformDirection(bodyPart.rigidbody.transform.up);
            var refOrientation = _referenceCharacter.hips.InverseTransformDirection(refbodyPart.transform.up);
            float diff = Vector3.SqrMagnitude(orientation - refOrientation);

            diffSquaredSum += diff;
        }
        return Mathf.Exp(weight * diffSquaredSum);
    }

    private float GetVelocityReward(float weight = -0.08f)
    {
        float diffSquaredSum = 0f;
        for (int i = 0; i < _jointDriveController.bodyPartList.Count; i++)
        {
            var bodyPart = _jointDriveController.bodyPartList[i];
            if (bodyPart.dofCount == 0) continue;

            var refbodyPart = _referenceCharacter.bodyPartList[i];

            // Velocity Error
            var velocity = bodyPart.rigidbody.angularVelocity;
            var refVelocity = refbodyPart.AngularVelocity;

            diffSquaredSum += Vector3.SqrMagnitude(velocity - refVelocity);
        }
        return Mathf.Exp(weight * diffSquaredSum);
    }

    private float GetEndEffectorReward(float weight = -20f)
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

            // Position Error
            var position = _hips.InverseTransformPoint(endEffector.position);
            var refPosition = _referenceCharacter.hips.InverseTransformPoint(refEndEffector.position);

            diffSquaredSum += Vector3.SqrMagnitude(position - refPosition);
        }
        return Mathf.Exp(weight * diffSquaredSum);
    }

    /// <summary>
    /// 힘을 적게 쓰면 높은 보상
    /// 상체가 안정적일 때(기준: 머리와 골반의 움직임 차이) 높은 보상
    /// </summary>
    /// <returns></returns>
    private float GetBalanceReward()
    {
        float energeSquaredSum = 0f;
        foreach (var bodyPart in _jointDriveController.bodyPartList)
        {
            if (bodyPart.dofCount == 0) continue;
            energeSquaredSum += Mathf.Pow(bodyPart.currentStrength / _jointDriveController.maxJointForce, 2f);
        }
        float energeReward = Mathf.Exp(-0.5f * energeSquaredSum);

        // float hipsVerticalVelocity = _jointDriveController.bodyPartDict[_hips].rigidbody.linearVelocity.y;
        // float stabilityReward = Mathf.Exp(-2f * hipsVerticalVelocity * hipsVerticalVelocity);

        var hipsVelocity = _jointDriveController.bodyPartDict[_hips].rigidbody.linearVelocity;
        var headVelocity = _jointDriveController.bodyPartDict[_head].rigidbody.linearVelocity;
        float stabilityReward = Mathf.Exp(-0.5f * Vector3.SqrMagnitude(hipsVelocity - headVelocity));

        return 0.5f * energeReward + 0.5f * stabilityReward;
    }

    /// <summary>
    /// 목표 속도와 실제 속도의 일치 정도에 따른 보상
    /// </summary>
    private float GetMatchingVelocityReward(Vector3 targetVelocity, Vector3 actualVelocity)
    {
        var diff = Mathf.Clamp(Vector3.Distance(actualVelocity, targetVelocity), 0f, TargetWalkingSpeed);
        var matchSpeedReward = Mathf.Pow(1f - Mathf.Pow(diff / TargetWalkingSpeed, 2f), 2f);

        // Check for NaNs
        if (float.IsNaN(matchSpeedReward))
        {
            throw new ArgumentException(
                "NaN in matchSpeedReward.\n" +
                $" targetd Velocity: {targetVelocity}\n" +
                $" hips.velocity: {_jointDriveController.bodyPartDict[_hips].rigidbody.linearVelocity}\n" +
                $" maximum walking speed: {_maxWalkingSpeed}"
            );
        }

        return matchSpeedReward;
    }

    /// <summary>
    /// 머리 방향과 목표 방향의 일치 정도에 따른 보상
    /// </summary>
    private float GetTargetHeadingReward(Vector3 targetDirection, Vector3 headForward)
    {
        var lookAtTargetReward = (Vector3.Dot(targetDirection, headForward) + 1) * 0.5f;

        //Check for NaNs
        if (float.IsNaN(lookAtTargetReward))
        {
            throw new ArgumentException(
                "NaN in lookAtTargetReward.\n" +
                $" localForward: {targetDirection}\n" +
                $" head.forward: {headForward}"
            );
        }

        return lookAtTargetReward;
    }

    // /// <summary>
    // /// 걷기 학습 시 추가되는 보상 함수
    // /// 레퍼런스 모션과 비교하여 발이 지면에 닿은 상태를 일치하도록 유도
    // /// </summary>
    // private float GetFootGroundingReward(BodyPart footL, BodyPart footR)
    // {
    //     if (!_useReferenceMotion) return 1f;

    //     var isMatchedL = footL.contactChecker.isTouchingGround == _referenceCharacter.footL.isTouchingGround;
    //     var isMatchedR = footR.contactChecker.isTouchingGround == _referenceCharacter.footR.isTouchingGround;

    //     var footReward = 0f;
    //     if (isMatchedL) footReward += 0.5f;
    //     if (isMatchedR) footReward += 0.5f;

    //     footReward *= footReward;
    //     // footReward = (isMatchedL && isMatchedR) ? 1f : 0f;

    //     return footReward;
    // }

    /// <summary>
    /// 목표 오브젝트와 충돌 시 획득하는 보상
    /// </summary>
    public void TouchedTarget()
    {
#if UNITY_EDITOR
        Debug.Log("Called Touched Target");
#endif
        AddReward(_targetTouchReward);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // base.Heuristic(actionsOut);
    }

    private void FixedUpdate()
    {
        UpdateOrientation();
        if (_useReferenceMotion) _referenceCharacter.Tick(Time.fixedDeltaTime, _animationSpeed);

        // 보상 지급
        // 모방 보상
        var imitationReward = _useReferenceMotion ? GetImitationReward() : 0f;
        // 균형 보상
        var balanceReward = GetBalanceReward();
        // 목표 보상
        var localForward = _localFrameController.transform.forward;
        var matchingVelocityReward = GetMatchingVelocityReward(TargetWalkingSpeed * localForward, GetAverageVelocity());
        var targetHeadingReward = GetTargetHeadingReward(localForward, _head.forward);
        var taskReward = matchingVelocityReward * targetHeadingReward;
        // // 추가: 발을 떼도록 유도
        // var footReward = GetFootGroundingReward(_jointDriveController.bodyPartDict[_footL], _jointDriveController.bodyPartDict[_footR]);

        var reward = !_useReferenceMotion ? (0.2f * balanceReward + 0.8f * taskReward) :
            (0.5f * imitationReward
            + 0.5f * taskReward);

        AddReward(reward);

#if UNITY_EDITOR
        _debugLog = "";
        _debugLog += $"current step: {Academy.Instance.StepCount}\n";
        _debugLog += $"current speed: {GetAverageVelocity().magnitude}\n";
        if (_useReferenceMotion) _debugLog += $"imitation reward: {imitationReward:F5}\n";
        if (!_useReferenceMotion && isTestMode) _debugLog += $"imitation reward: {GetImitationReward():F5}\n";
        _debugLog += $"balance reward: {balanceReward:F5}\n";
        _debugLog += $"velocity reward: {matchingVelocityReward:F5}\n";
        _debugLog += $"heading reward: {targetHeadingReward:F5}\n";
        _debugLog += $"task reward: {taskReward:F5}\n";
        // _debugLog += $"foot grounding reward: {footReward:F5}\n";
        _debugLog += $"total reward: {reward:F5}\n";
#endif
    }

    // 테스트용 함수
#if UNITY_EDITOR
    public void TestRSI()
    {
        _jointDriveController.RandomSampleInitialize(_referenceCharacter, _currentSkill);
        _hips.rotation = Quaternion.identity;
    }

    public void TestCopy()
    {
        _jointDriveController.CopyReferencePose(_referenceCharacter);
    }

    public void TestResetPose()
    {
        _jointDriveController.Reset();
        _hips.rotation = Quaternion.identity;
    }

    public void TestStopPlaying()
    {
        _useReferenceMotion = !_useReferenceMotion;
    }
#endif
}
