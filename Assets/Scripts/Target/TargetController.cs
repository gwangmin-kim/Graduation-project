using UnityEngine;
using Random = UnityEngine.Random;
using UnityEngine.Events;

public class TargetController : MonoBehaviour
{

    [Header("Collider Tag To Detect")]
    public string tagToDetect = "Player";

    [Header("Target Placement")]
    [SerializeField] private float _spawnRadius;
    [SerializeField] private bool _respawnOnTouched = true;
    [SerializeField] private bool _respawnPeriodically = false;
    [Range(_minRespawnInterval, _maxRespawnInterval)][SerializeField] private float _respawnInterval = _maxRespawnInterval;


    [Header("Target Fell Protection")]
    [SerializeField] private bool _respawnOnFallOff = true;
    [SerializeField] private float _fallDistance = 5;

    private Vector3 _initLocalPosition;
    private float _respawnTimer = 0f;
    private const float _minRespawnInterval = 1.0f;
    private const float _maxRespawnInterval = 10.0f;

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

    // Start is called before the first frame update
    void OnEnable()
    {
        _initLocalPosition = transform.localPosition;
        if (_respawnOnTouched)
        {
            MoveTargetToRandomPosition();
        }
        if (_respawnPeriodically)
        {
            _respawnInterval = Random.Range(_minRespawnInterval, _maxRespawnInterval);
            _respawnTimer = _respawnInterval;
        }
    }

    void Update()
    {
        if (_respawnOnFallOff)
        {
            if (transform.localPosition.y < _initLocalPosition.y - _fallDistance)
            {
                Debug.Log($"{transform.name} Fell Off Platform");
                MoveTargetToRandomPosition();
            }
        }
        if (_respawnPeriodically)
        {
            _respawnTimer -= Time.deltaTime;
            if (_respawnTimer <= 0f)
            {
                MoveTargetToRandomPosition();
                _respawnInterval = Random.Range(_minRespawnInterval, _maxRespawnInterval);
                _respawnTimer = _respawnInterval;
            }
        }
    }

    public void MoveTargetToRandomPosition()
    {
        var newTargetPosition = _initLocalPosition + (Random.insideUnitSphere * _spawnRadius);
        newTargetPosition.y = _initLocalPosition.y;
        transform.localPosition = newTargetPosition;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.transform.CompareTag(tagToDetect))
        {
            onCollisionEnterEvent.Invoke(collision);
            if (_respawnOnTouched)
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

    private void OnCollisionExit(Collision collision)
    {
        if (collision.transform.CompareTag(tagToDetect))
        {
            onCollisionExitEvent.Invoke(collision);
        }
    }

    private void OnTriggerEnter(Collider collider)
    {
        if (collider.CompareTag(tagToDetect))
        {
            onTriggerEnterEvent.Invoke(collider);
            if (_respawnOnTouched)
            {
                MoveTargetToRandomPosition();
            }
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

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        Gizmos.color = Color.yellowGreen;
        Gizmos.DrawWireSphere(transform.parent.TransformPoint(_initLocalPosition), _spawnRadius);
#endif
    }
}
