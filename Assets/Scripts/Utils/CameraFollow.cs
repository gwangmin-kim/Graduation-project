using UnityEngine;
public class CameraFollow : MonoBehaviour
{
    [Tooltip("The target to follow")] public Transform target;

    [Tooltip("The time it takes to move to the new position")]
    public float smoothingTime; //The time it takes to move to the new position

    private Vector3 _offset;
    private Vector3 _camVelocity; //Camera's velocity (used by SmoothDamp)

    // Use this for initialization
    void Start()
    {
        _offset = transform.position - target.position;
        transform.rotation = Quaternion.LookRotation(-_offset);
    }

    void FixedUpdate()
    {
        var newPosition = new Vector3(
            target.position.x + _offset.x, transform.position.y, target.position.z + _offset.z);

        transform.position = Vector3.SmoothDamp(
                transform.position, newPosition, ref _camVelocity, smoothingTime, Mathf.Infinity, Time.fixedDeltaTime);
    }
}
