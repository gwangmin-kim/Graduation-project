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
    public Transform hips; // 루트

    // 말단 부위 (L-hand, R-hand, L-foot, R-foot 순서)
    public List<Transform> endEffectorList;

    public List<ReferenceBodyPart> childList = new List<ReferenceBodyPart>();

    // animation
    private ReferenceAnimationManager _referenceAnimationManager;
    private Animator _animator;
    public string currentClipName;

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
            if (body.transform == hips) continue;
            childList.Add(body);
        }

        _animator.enabled = true;
    }

    private bool _isPlaying = false;
    private void Update()
    {
        if (_isPlaying) Tick(Time.deltaTime);
    }
    public void TogglePlay()
    {
        _isPlaying = !_isPlaying;
    }

    public void InitPose(Skill skill, float initPhase)
    {
        currentClip = _referenceAnimationManager.GetClipFromSkill(skill);
        currentClipName = skill.ToString();
        CurrentPhase = initPhase;

        _animator.Play(currentClipName, 0, CurrentPhase);
        _animator.Update(0);

        foreach (var body in childList)
        {
            body.UpdateJointPosition();
            body.ResetVelocities();
        }
    }

    public void SetPhase(float phase)
    {
        CurrentPhase = Mathf.Clamp01(phase);
        _animator.Play(currentClipName, 0, CurrentPhase);
    }

    public void Tick(float deltaTime)
    {
        // 위상 전진
        CurrentPhase = (CurrentPhase + (deltaTime / currentClip.length)) % 1f;

        // 동작 샘플링
        _animator.Play(currentClipName, 0, CurrentPhase);
        _animator.Update(0);

        // 속도 업데이트
        foreach (var body in childList)
        {
            body.UpdateJointPosition();
            body.UpdateVelocities(deltaTime);
        }
    }
}
