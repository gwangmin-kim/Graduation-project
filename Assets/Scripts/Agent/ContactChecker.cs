using UnityEngine;
using Unity.MLAgents;

[DisallowMultipleComponent]
public class ContectChecker : MonoBehaviour
{
    [HideInInspector] public Agent agent;

    [Header("Ground Check")]
    public bool earlyTerminateOnGroundContact = true; // 지면에 닿았을 때 에이전트 초기화 여부
    public bool penalizeOnGroundContact = true; // 지면에 닿았을 때 페널티 부여 여부
    public float groundContactPenalty = -1f; // 페널티 강도 (ex: -1)
    public bool isTouchingGround = false;
    private const string _groundTag = "Ground";

    [Header("Obstacle Check")]
    public bool penalizeOnObstacleContact = true; // 장애물에 닿았을 때 페널티 부여 여부
    public float obstacleContactPenalty = -1f; // 페널티 강도 (ex: -1)
    public bool isTouchingObstacle = false;
    private const string _obstacleTag = "Obstacle";

    private void OnCollisionEnter(Collision other)
    {
        if (other.transform.CompareTag(_groundTag))
        {
            isTouchingGround = true;

#if UNITY_EDITOR
            Debug.Log($"{name} touched the ground");
#endif

            if (penalizeOnGroundContact)
            {
                agent.SetReward(groundContactPenalty);
            }

            if (earlyTerminateOnGroundContact)
            {
                agent.EndEpisode();
            }
        }

        else if (other.transform.CompareTag(_obstacleTag))
        {
            isTouchingObstacle = true;

#if UNITY_EDITOR
            Debug.Log($"{name} touched the obstacle");
#endif

            if (penalizeOnObstacleContact)
            {
                agent.AddReward(obstacleContactPenalty);
            }
        }
    }

    private void OnCollisionExit(Collision other)
    {
        if (other.transform.CompareTag(_groundTag))
        {
            isTouchingGround = false;

#if UNITY_EDITOR
            Debug.Log($"{name} detatched from the ground");
#endif
        }

        else if (other.transform.CompareTag(_obstacleTag))
        {
            isTouchingObstacle = false;

#if UNITY_EDITOR
            Debug.Log($"{name} detatched from the obstacle");
#endif
        }
    }

    private void OnCollisionStay(Collision other)
    {
        if (other.transform.CompareTag(_groundTag))
        {
            isTouchingGround = true;
        }
    }
}
