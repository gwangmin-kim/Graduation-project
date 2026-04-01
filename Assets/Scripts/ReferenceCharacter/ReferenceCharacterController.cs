using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ReferenceAnimationManager))]
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

    private void Awake()
    {
        _referenceAnimationManager = GetComponent<ReferenceAnimationManager>();
    }

    private void Start()
    {
        var bodyList = hips.GetComponentsInChildren<ReferenceBodyPart>();
        foreach (var body in bodyList)
        {
            if (body.transform == hips) continue;
            childList.Add(body);
        }
    }

    public void InitPose(Skill skill, float initPhase)
    {
        var clip = _referenceAnimationManager.GetClipFromSkill(skill);

        CurrentPhase = Mathf.Clamp01(initPhase);
        float sampleTime = CurrentPhase * clip.length;

        clip.SampleAnimation(gameObject, sampleTime);
        currentClip = clip;

        foreach (var body in childList)
        {
            body.UpdateJointPosition();
            body.ResetVelocities();
        }
    }

    public void SetPhase(float phase)
    {
        CurrentPhase = Mathf.Clamp01(phase);
        ApplyPhase();
    }

    private void ApplyPhase()
    {
        float sampleTime = CurrentPhase * currentClip.length;
        currentClip.SampleAnimation(gameObject, sampleTime);
    }

    public void Tick(float deltaTime)
    {
        // 위상 전진
        CurrentPhase = (CurrentPhase + (deltaTime / currentClip.length)) % 1f;

        // 동작 샘플링
        ApplyPhase();

        // 속도 업데이트
        foreach (var body in childList)
        {
            body.UpdateVelocities(deltaTime);
        }
    }
}
