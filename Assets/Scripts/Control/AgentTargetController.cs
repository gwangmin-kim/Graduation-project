using UnityEngine;

public class AgentTargetController : MonoBehaviour
{
    [Header("Origin")]
    public Transform playerOrigin;
    public Transform target;
    private Vector3 _targetPosition = Vector3.zero;

    [Header("Calc Target Position")]
    public LayerMask collisionLayer;
    public float maxDistance = 10f;
    public float updateInterval = 0.1f;
    public float smoothTime = 0.3f;

    private float _updateTimer = 0f;
    private Vector3 _velocity = Vector3.zero;

    private void Update()
    {
        _updateTimer -= Time.deltaTime;
        if (_updateTimer < 0f)
        {
            _updateTimer = updateInterval;
            UpdateTargetPosition();
        }

        MoveTarget();
    }

    private void UpdateTargetPosition()
    {
        var rawInput = InputManager.Instance.move;
        var move = new Vector3(rawInput.x, 0f, rawInput.y);
        var direction = playerOrigin.TransformDirection(move);

        direction.y = 0f;
        direction = direction.normalized;

        var ray = new Ray(playerOrigin.position, direction);
        if (Physics.Raycast(ray, out var hitInfo, maxDistance, collisionLayer))
        {
            _targetPosition = hitInfo.point;
        }
        else
        {
            _targetPosition = playerOrigin.position + direction * maxDistance;
        }
    }

    private void MoveTarget()
    {
        target.position = Vector3.SmoothDamp(target.position, _targetPosition, ref _velocity, smoothTime);
    }
}
