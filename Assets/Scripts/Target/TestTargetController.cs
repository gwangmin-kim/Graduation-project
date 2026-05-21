using UnityEngine;

public class TestTargetController : MonoBehaviour
{
    public static TestTargetController Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    // 동일한 상황에서의 모델 간 비교를 위해, 랜덤성을 제거하고 타겟을 정해진 규칙대로 이동시키는 로직
    [Header("General Settings")]
    [SerializeField] private Vector2 _intervalRange;
    [SerializeField] private Transform _target;
    [SerializeField] private Transform _nextTarget;

    [Header("Current Settings")]
    [SerializeField] private Transform _targetRoot;
    [SerializeField] private Transform[] _positions; // targetRoot에 달린 자식 오브젝트: 타겟이 스폰되는 지점들
    [SerializeField] private float[] _intervals; // 타겟이 다음 스폰 지점으로 이동하기까지의 간격

    [SerializeField, HideInInspector]
    private int _count;
    private int _currentIndex;
    private int NextIndex => (_currentIndex + 1) % _count;

    private float _respawnTimer = 0f;

    private void OnValidate()
    {
        if (_targetRoot == null) return;

        _count = _targetRoot.childCount;

        // 배열의 크기가 다를 때만 재할당하여, 인스펙터에서 수동으로 조절한 간격(_intervals) 값이
        // 스크립트를 수정할 때마다 초기화되는 것을 방지합니다.
        if (_positions == null || _positions.Length != _count)
        {
            _positions = new Transform[_count];
            _intervals = new float[_count];

            for (int i = 0; i < _count; i++)
            {
                _positions[i] = _targetRoot.GetChild(i);
                _intervals[i] = Random.Range(_intervalRange.x, _intervalRange.y);
            }
        }
        else
        {
            // 자식 오브젝트의 순서나 트랜스폼이 바뀌었을 수 있으므로 포지션은 매번 업데이트
            for (int i = 0; i < _count; i++)
            {
                _positions[i] = _targetRoot.GetChild(i);
            }
        }
    }

    private void Start()
    {
        if (_count < 2)
        {
            Debug.LogError("TestTargetController: 경로를 구성하려면 최소 2개 이상의 타겟 지점이 필요합니다.");
            return;
        }

        Initialize();
    }

    public void Initialize()
    {
        // 1. 초기 인덱스 및 타이머 설정
        _currentIndex = 0;
        _respawnTimer = _intervals[_currentIndex];

        // 2. Target과 NextTarget의 초기 위치 설정
        // Target은 현재 인덱스의 위치에, NextTarget은 다음 인덱스의 위치에 스폰
        _target.position = _positions[_currentIndex].position;
        _nextTarget.position = _positions[NextIndex].position;
    }

    private void Update()
    {
        if (_count < 2) return;

        _respawnTimer -= Time.deltaTime;

        if (_respawnTimer <= 0f)
        {
            MoveToNextPosition();
        }
    }

    public void MoveToNextPosition()
    {
        // 1. 현재 타겟은 미리 예고되었던 다음 타겟(NextTarget)의 위치로 이동
        _target.position = _nextTarget.position;

        // 2. 인덱스 업데이트 (다음 지점을 향해 한 칸 전진)
        _currentIndex = NextIndex;

        // 3. 다음 타겟(NextTarget)은 새롭게 갱신된 NextIndex의 위치로 먼저 이동하여 대기
        _nextTarget.position = _positions[NextIndex].position;

        // 4. 타이머 초기화 (새로 도착한 목표 지점에서의 대기 시간 적용)
        _respawnTimer = _intervals[_currentIndex];
    }

    // 에디터 상에서 경로를 시각적으로 확인하기 위한 기즈모 (논문용 캡처 시 유용함)
    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        if (_positions == null || _positions.Length < 2) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < _count; i++)
        {
            int next = (i + 1) % _count;
            if (_positions[i] != null && _positions[next] != null)
            {
                // 각 목표 지점을 구형으로 표시
                Gizmos.DrawWireSphere(_positions[i].position, 0.5f);
                // 목표 지점 간의 이동 경로를 선으로 표시
                Gizmos.DrawLine(_positions[i].position, _positions[next].position);
            }
        }
#endif
    }
}
