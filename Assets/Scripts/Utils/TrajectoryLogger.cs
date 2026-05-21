using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class TrajectoryLogger : MonoBehaviour
{
    public static TrajectoryLogger Instance { get; private set; }

    public Transform hipTransform;     // 에이전트의 Hip Transform
    public Transform targetTransform;  // 목표(Target) Transform
    public string modelName = "Phase1"; // 저장될 파일 이름 구분을 위해 (Phase1, Phase2)

    private List<Vector2> _hipPositions = new List<Vector2>();
    private List<Vector2> _targetPositions = new List<Vector2>();
    private bool _isRecording = false;

    private void Awake()
    {
        Instance = this;
    }

    // 에피소드가 시작할 때 호출합니다 (Agent의 OnEpisodeBegin에서 호출)
    public void StartRecording()
    {
        _hipPositions.Clear();
        _targetPositions.Clear();
        _isRecording = true;
    }

    // 매 물리 스텝마다 호출하여 좌표를 기록합니다 (Agent의 FixedUpdate 또는 OnActionReceived에서 호출)
    public void RecordStep()
    {
        if (_isRecording && hipTransform != null && targetTransform != null)
        {
            // Y좌표(높이)는 무시하고 X와 Z만 2D 평면 좌표로 저장합니다.
            _hipPositions.Add(new Vector2(hipTransform.position.x, hipTransform.position.z));
            _targetPositions.Add(new Vector2(targetTransform.position.x, targetTransform.position.z));
        }
    }

    // 에피소드가 끝날 때(넘어지거나 Max Step 도달 시) 호출하여 CSV로 내보냅니다.
    public void SaveTrajectory()
    {
        if (!_isRecording || _hipPositions.Count == 0) return;
        _isRecording = false;

        string filePath = Path.Combine(Application.dataPath, $"Trajectory_{modelName}_{System.DateTime.Now:yyyyMMddHHmmss}.csv");

        using (StreamWriter writer = new StreamWriter(filePath))
        {
            writer.WriteLine("Step,Hip_X,Hip_Z,Target_X,Target_Z");
            for (int i = 0; i < _hipPositions.Count; i++)
            {
                writer.WriteLine($"{i},{_hipPositions[i].x},{_hipPositions[i].y},{_targetPositions[i].x},{_targetPositions[i].y}");
            }
        }
        Debug.Log($"궤적 데이터가 저장되었습니다: {filePath}");
    }
}
