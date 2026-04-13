using UnityEngine;

public class ReferenceGroundChecker : MonoBehaviour
{
    [Header("Toe & Heel")]
    [SerializeField] private Transform _toe;
    [SerializeField] private Transform _heel;

    [Header("Ground Check")]
    [SerializeField] private float _checkHeight;
    [SerializeField] private LayerMask _groundLayer;

    public bool isTouchingGround = false;

    private void FixedUpdate()
    {
        // bool toeGrounded = Physics.Raycast(_toe.position, Vector3.down, _checkDistance, _groundLayer, QueryTriggerInteraction.Ignore);
        // bool heelGrounded = Physics.Raycast(_heel.position, Vector3.down, _checkDistance, _groundLayer, QueryTriggerInteraction.Ignore);

        bool toeGrounded = _toe.position.y <= _checkHeight;
        bool heelGrounded = _heel.position.y <= _checkHeight;

        isTouchingGround = toeGrounded || heelGrounded;
    }
}
