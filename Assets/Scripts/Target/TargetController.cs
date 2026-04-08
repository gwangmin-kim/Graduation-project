using UnityEngine;
using UnityEngine.Events;

public class TargetController : MonoBehaviour
{
    [Header("Tag to Detect")]
    public const string tagToDetect = "Player";

    [Header("Target Placement")]
    public float spawnRadius;
    public bool respawnOnTouched = true;

    [Header("Target Fell Protection")]
    public bool respawnOnFallOff = true;
    public float fallDistance = 5f;

    private Vector3 _initLocalPosition;

    [System.Serializable]
    public class TriggerEvent : UnityEvent<Collider>
    {
    }

    [Header("Trigger Callbacks")]
    public TriggerEvent onTriggerEnterEvent = new TriggerEvent();
    public TriggerEvent onTriggerStayEvent = new TriggerEvent();
    public TriggerEvent onTriggerExitEvent = new TriggerEvent();

    [System.Serializable]
    public class CollisionEvent : UnityEvent<Collision>
    {
    }

    [Header("Collision Callbacks")]
    public CollisionEvent onCollisionEnterEvent = new CollisionEvent();
    public CollisionEvent onCollisionStayEvent = new CollisionEvent();
    public CollisionEvent onCollisionExitEvent = new CollisionEvent();

    private static Vector3 RandomPointInBounds(Bounds bounds) => new Vector3(
        Random.Range(bounds.min.x, bounds.max.x),
        Random.Range(bounds.min.y, bounds.max.y),
        Random.Range(bounds.min.z, bounds.max.z)
    );

    public void MoveTargetToRandomPosition()
    {
        var newPosition = _initLocalPosition + (Random.insideUnitSphere * spawnRadius);
        newPosition.y = _initLocalPosition.y;
        transform.localPosition = newPosition;
    }

    private void OnEnable()
    {
        _initLocalPosition = transform.localPosition;
        if (respawnOnTouched)
        {
            MoveTargetToRandomPosition();
        }
    }

    private void Update()
    {
        if (respawnOnFallOff)
        {
            if (transform.localPosition.y < _initLocalPosition.y - fallDistance)
            {
#if UNITY_EDITOR
                Debug.Log($"{transform.name} Fell Off Platform");
#endif
                MoveTargetToRandomPosition();
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.transform.CompareTag(tagToDetect))
        {
            onCollisionEnterEvent.Invoke(collision);

            if (respawnOnTouched)
            {
                MoveTargetToRandomPosition();
            }
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.transform.CompareTag(tagToDetect))
        {
            onCollisionStayEvent.Invoke(collision);
        }
    }

    private void OnCollisionExit(Collision collsion)
    {
        if (collsion.transform.CompareTag(tagToDetect))
        {
            onCollisionExitEvent.Invoke(collsion);
        }
    }

    private void OnTriggerEnter(Collider collider)
    {
        if (collider.CompareTag(tagToDetect))
        {
            onTriggerEnterEvent.Invoke(collider);
        }
    }

    private void OnTriggerStay(Collider collider)
    {
        if (collider.CompareTag(tagToDetect))
        {
            onTriggerStayEvent.Invoke(collider);
        }
    }

    private void OnTriggerExit(Collider collider)
    {
        if (collider.CompareTag(tagToDetect))
        {
            onTriggerExitEvent.Invoke(collider);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.greenYellow;
        Gizmos.DrawWireSphere(transform.parent.TransformPoint(_initLocalPosition), spawnRadius);
    }
#endif
}
