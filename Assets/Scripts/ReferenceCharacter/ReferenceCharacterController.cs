using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ReferenceAnimationManager))]
[RequireComponent(typeof(Animator))]
public class ReferenceCharacterController : MonoBehaviour
{
    [Header("Current States")]
    public float CurrentPhase { get; private set; }
    public AnimationClip currentClip;

    [Header("Body Parts")]
    public Transform hips;
    // 지면 판별용
    public ReferenceGroundChecker footL;
    public ReferenceGroundChecker footR;
    // 전체 신체 부위
    // 반드시 에이전트와 동일한 순서의 계층 구조를 가지고 있어야 함
    public List<ReferenceBodyPart> bodyPartList = new List<ReferenceBodyPart>();
    // 말단 부위 (L-hand, R-hand, L-foot, R-foot 순서)
    public List<Transform> endEffectorList;

    // animation
    private ReferenceAnimationManager _referenceAnimationManager;
    private Animator _animator;
    private string _currentClipName;

    private void Awake()
    {
        _referenceAnimationManager = GetComponent<ReferenceAnimationManager>();

        _animator = GetComponent<Animator>();
        _animator.speed = 0; // 자동 재생 방지
        _animator.enabled = false;
    }

    private void Start()
    {
        var bodyList = hips.GetComponentsInChildren<ReferenceBodyPart>();
        foreach (var body in bodyList)
        {
            bodyPartList.Add(body);
        }

        _animator.enabled = true;
    }

    public void InitPose(Skill skill, float initPhase)
    {
        currentClip = _referenceAnimationManager.GetClipFromSkill(skill);
        _currentClipName = skill.ToString();
        CurrentPhase = initPhase;

        _animator.Play(_currentClipName, 0, CurrentPhase);
        _animator.Update(0);

        foreach (var body in bodyPartList)
        {
            body.ResetVelocities();
        }
    }

    public void SetPhase(float phase)
    {
        CurrentPhase = Mathf.Clamp01(phase);
        _animator.Play(_currentClipName, 0, CurrentPhase);
    }

    public void Tick(float deltaTime, float speedRate = 1f)
    {
        // 위상 전진
        CurrentPhase = (CurrentPhase + (deltaTime * speedRate / currentClip.length)) % 1f;

        // 동작 샘플링
        _animator.Play(_currentClipName, 0, CurrentPhase);
        _animator.Update(0);

        // 속도 업데이트
        foreach (var body in bodyPartList)
        {
            body.UpdateVelocities(deltaTime);
        }
    }
}
