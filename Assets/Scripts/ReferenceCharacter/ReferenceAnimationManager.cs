using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct SkillClipEntry
{
    public Skill skill;
    public AnimationClip clip;
}

public class ReferenceAnimationManager : MonoBehaviour
{
    public List<SkillClipEntry> clipList = new List<SkillClipEntry>();
    private Dictionary<Skill, AnimationClip> _skillClipDict = new Dictionary<Skill, AnimationClip>();

    public AnimationClip GetClipFromSkill(Skill skill)
    {
        if (!_skillClipDict.TryGetValue(skill, out var clip))
        {
            Debug.LogWarning($"There are no clip corresponding to skill: {skill}");
            return null;
        }
        return clip;
    }

    private void Awake()
    {
        foreach (var entry in clipList)
        {
            _skillClipDict.Add(entry.skill, entry.clip);
        }
    }
}
