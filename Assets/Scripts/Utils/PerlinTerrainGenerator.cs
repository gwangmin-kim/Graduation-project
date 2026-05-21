using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class PerlinTerrainGenerator : MonoBehaviour
{
    [Header("지형 크기 설정")]
    public int xSize = 20; // 가로 세그먼트 수
    public int zSize = 20; // 세로 세그먼트 수
    public float cellSize = 1f; // 격자 한 칸의 실제 크기

    [Header("펄린 노이즈 설정")]
    [Tooltip("값이 작을수록 완만하고 큰 굴곡이 생기고, 클수록 촘촘하고 자잘한 굴곡이 생깁니다.")]
    public float scale = 0.3f;

    [Tooltip("지형의 최대 높이입니다. 미약한 효과를 위해 낮게 시작하는 것을 추천합니다.")]
    public float magnitude = 0.5f;

    [Header("시드 설정 (패턴 변경)")]
    public float offsetX = 0f;
    public float offsetZ = 0f;

    private Mesh _mesh;
    private Vector3[] _vertices;
    private int[] _triangles;

    // 에디터 컴포넌트 메뉴에 "Generate Terrain" 버튼을 추가합니다.
    [ContextMenu("Generate Terrain")]
    public void GenerateTerrain()
    {
        _mesh = new Mesh();
        _mesh.name = "Procedural Noise Terrain";
        GetComponent<MeshFilter>().mesh = _mesh;

        CreateShape();
        UpdateMesh();
    }

    void CreateShape()
    {
        // 1. 정점(Vertices) 생성
        _vertices = new Vector3[(xSize + 1) * (zSize + 1)];

        for (int i = 0, z = 0; z <= zSize; z++)
        {
            for (int x = 0; x <= xSize; x++)
            {
                // 입력된 scale과 offset을 반영하여 펄린 노이즈 기반의 y(높이)값 계산
                float xCoord = (x * scale) + offsetX;
                float zCoord = (z * scale) + offsetZ;
                float y = Mathf.PerlinNoise(xCoord, zCoord) * magnitude;

                // cellSize를 곱해 전체적인 지형 크기 조절
                _vertices[i] = new Vector3(x * cellSize, y, z * cellSize);
                i++;
            }
        }

        // 2. 삼각형(Triangles) 인덱스 매핑 (시계방향 렌더링)
        _triangles = new int[xSize * zSize * 6];
        int vert = 0;
        int tris = 0;

        for (int z = 0; z < zSize; z++)
        {
            for (int x = 0; x < xSize; x++)
            {
                _triangles[tris + 0] = vert + 0;
                _triangles[tris + 1] = vert + xSize + 1;
                _triangles[tris + 2] = vert + 1;
                _triangles[tris + 3] = vert + 1;
                _triangles[tris + 4] = vert + xSize + 1;
                _triangles[tris + 5] = vert + xSize + 2;

                vert++;
                tris += 6;
            }
            vert++;
        }
    }

    void UpdateMesh()
    {
        _mesh.Clear();
        _mesh.vertices = _vertices;
        _mesh.triangles = _triangles;
        _mesh.RecalculateNormals(); // 빛 반사 및 물리 연산을 위해 필수

        // 에이전트가 충돌을 감지할 수 있도록 콜라이더 실시간 업데이트
        MeshCollider meshCollider = GetComponent<MeshCollider>();
        meshCollider.sharedMesh = _mesh;
    }
}
