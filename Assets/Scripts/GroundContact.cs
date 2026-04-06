using UnityEngine;
using Unity.MLAgents;

[DisallowMultipleComponent]
public class GroundContact : MonoBehaviour
{
    [HideInInspector] public Agent agent;

    [Header("Ground Check")]
    public bool earlyTerminateOnGroundContact = true; // 지면에 닿았을 때 에이전트 초기화 여부
    public bool penalizeOnGroundContact = true; // 지면에 닿았을 때 페널티 부여 여부
    public float groundContactPenalty = -1f; // 페널티 강도 (ex: -1)
    public bool isTouchingGround = false;
    const string _groundTag = "Ground";

    private void OnCollisionEnter(Collision other)
    {
        if (other.transform.CompareTag(_groundTag))
        {
            isTouchingGround = true;
            if (penalizeOnGroundContact)
            {
                agent.SetReward(groundContactPenalty);
            }

            if (earlyTerminateOnGroundContact)
            {
                agent.EndEpisode();
            }
        }
    }

    private void OnCollisionExit(Collision other)
    {
        if (other.transform.CompareTag(_groundTag))
        {
            isTouchingGround = false;
        }
    }
}
