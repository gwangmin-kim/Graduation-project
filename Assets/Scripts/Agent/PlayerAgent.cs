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

    [Header("Walk")]
    [SerializeField] private Transform _target;
    [SerializeField][Range(_minWalkingSpeed, _maxWalkingSpeed)] private float _targetWalkingSpeed = _maxWalkingSpeed;
    [SerializeField] private Vector3 _targetDirection = Vector3.forward;
    [SerializeField] private bool _randomizeWalkSpeedEachEpisode;

    private const float _minWalkingSpeed = 0.1f;
    private const float _maxWalkingSpeed = 10f;
    private Vector3 _worldDirectionToWalk = Vector3.forward;
    public float TargetWalkingSpeed
    {
        get { return _targetWalkingSpeed; }
        set { _targetWalkingSpeed = Mathf.Clamp(value, _minWalkingSpeed, _maxWalkingSpeed); }
    }

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
        _jointDriveController.Reset();
        _hips.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        TargetWalkingSpeed = _randomizeWalkSpeedEachEpisode ? Random.Range(_minWalkingSpeed, _maxWalkingSpeed) : TargetWalkingSpeed;

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
        if (bodyPart.rigidbody.transform != _hips)
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

        var localForward = _localFrameController.transform.forward;
        var currentVelocity = GetAverageVelocity();
        var targetVelocity = localForward * TargetWalkingSpeed;

        sensor.AddObservation(Vector3.Distance(targetVelocity, currentVelocity));
        sensor.AddObservation(_localFrameController.transform.InverseTransformDirection(currentVelocity));
        sensor.AddObservation(_localFrameController.transform.InverseTransformDirection(targetVelocity));

        sensor.AddObservation(_localFrameController.transform.InverseTransformPoint(_target.transform.position));

        // 전체 회전 상태
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

#if UNITY_EDITOR
        // Debug.Log(actionIndex);
#endif

        // // 생존 보상 (서 있는 것이 유리하도록)
        // AddReward(0.1f);
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

    /// <summary>
    /// 목표 속도와 실제 속도의 일치 정도에 따른 보상
    /// </summary>
    public float GetMatchingVelocityReward(Vector3 targetVelocity, Vector3 actualVelocity)
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
    public float GetTargetHeadingReward(Vector3 targetDirection, Vector3 headForward)
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

    /// <summary>
    /// 목표 오브젝트와 충돌 시 획득하는 보상
    /// </summary>
    public void TouchedTarget()
    {
#if UNITY_EDITOR
        Debug.Log("Called Touched Target");
#endif
        AddReward(1f);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // base.Heuristic(actionsOut);
    }

    private void FixedUpdate()
    {
        UpdateOrientation();

        // 보상 지급
        var localForward = _localFrameController.transform.forward;
        var matchingVelocityReward = GetMatchingVelocityReward(TargetWalkingSpeed * localForward, GetAverageVelocity());
        var targetHeadingReward = GetTargetHeadingReward(localForward, _head.forward);

        var reward = matchingVelocityReward * targetHeadingReward;
        AddReward(reward);

#if UNITY_EDITOR
        _debugLog = "";
        _debugLog += $"velocity reward: {matchingVelocityReward:F5}\n";
        _debugLog += $"heading reward: {targetHeadingReward:F5}\n";
        _debugLog += $"total reward: {reward:F5}\n";
#endif
    }
}
