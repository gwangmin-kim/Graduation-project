using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class PlayerAgent : Agent
{
    [Header("Body Parts")]
    public Transform hips;
    public Transform spine;
    public Transform chest;
    public Transform head;
    public Transform upperLegL;
    public Transform lowerLegL;
    public Transform footL;
    public Transform upperLegR;
    public Transform lowerLegR;
    public Transform footR;
    public Transform upperArmL;
    public Transform lowerArmL;
    public Transform handL;
    public Transform upperArmR;
    public Transform lowerArmR;
    public Transform handR;

    [Header("Walk")]
    [SerializeField][Range(0.1f, 10f)] private float m_TargetWalkingSpeed = 10f;
    public Vector2 targetDirection;

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void OnEpisodeBegin()
    {
        base.OnEpisodeBegin();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        base.CollectObservations(sensor);
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        base.OnActionReceived(actionBuffers);
    }

    private void FixedUpdate()
    {

    }
}
