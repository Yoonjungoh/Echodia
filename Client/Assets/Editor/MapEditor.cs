using System;
using System.IO;
using UnityEngine;
using UnityEditor;

public class MapEditor : EditorWindow
{
    public const float NO_HEIGHT_VALUE = -9999f;
    float cellSize = 2.0f;
    const float MapMinX = -2000f;
    const float MapMaxX = 2000f;
    const float MapMinZ = -2000f;
    const float MapMaxZ = 2000f;

    Vector3 origin;
    int sizeX;
    int sizeZ;

    int groundLayerIndex = 0;
    int blockLayerIndex = 0;

    string mapDataName = "";

    LayerMask groundMask;
    LayerMask blockMask;

    [MenuItem("Tools/GenerateMap %#q")]
    public static void ShowWindow()
    {
        GetWindow<MapEditor>("Map Generator");
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();
        cellSize = EditorGUILayout.FloatField("Cell Size", cellSize);

        groundLayerIndex = EditorGUILayout.LayerField("Ground Layer", groundLayerIndex);
        blockLayerIndex = EditorGUILayout.LayerField("Block Layer", blockLayerIndex);

        mapDataName = EditorGUILayout.TextField("Map Data Name", mapDataName);

        groundMask = 1 << groundLayerIndex;
        blockMask = 1 << blockLayerIndex;

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate Binary Map"))
            Generate();

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate Map Meta (Spawn Points)"))
            GenerateMeta();

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate All (Binary + Meta)"))
        {
            Generate();
            GenerateMeta();
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "씬에 배치된 컴포넌트:\n" +
            "• MapEnterPoint  - 순방향 진입 스폰 위치 (초록)\n" +
            "• MapLeavePoint  - 역방향 진입 스폰 위치 (주황)\n" +
            "• MonsterSpawner - 몬스터 스포너 (빨강)",
            MessageType.Info
        );
    }

    // ──────────────────────────────────────────────────────────────
    // Binary Map 생성 (기존)
    // ──────────────────────────────────────────────────────────────
    void Generate()
    {
        origin = new Vector3(MapMinX, 0, MapMinZ);
        sizeX = Mathf.CeilToInt((MapMaxX - MapMinX) / cellSize);
        sizeZ = Mathf.CeilToInt((MapMaxZ - MapMinZ) / cellSize);

        float[,] height = new float[sizeX, sizeZ];
        bool[,] canGo = new bool[sizeX, sizeZ];

        float rayStartY = 10000f;
        float rayLength = 20000f;

        for (int x = 0; x < sizeX; x++)
        {
            for (int z = 0; z < sizeZ; z++)
            {
                float worldX = origin.x + x * cellSize;
                float worldZ = origin.z + z * cellSize;

                Vector3 rayOrigin = new Vector3(worldX, rayStartY, worldZ);
                Ray ray = new Ray(rayOrigin, Vector3.down);

                if (Physics.Raycast(ray, out RaycastHit hit, rayLength, groundMask))
                {
                    height[x, z] = hit.point.y;

                    Vector3 capStart = hit.point + Vector3.up * 0.5f;
                    Vector3 capEnd = hit.point + Vector3.up * 1.5f;
                    float radius = 0.3f;

                    bool blocked = false;

                    Collider[] cols = Physics.OverlapCapsule(capStart, capEnd, radius, blockMask);

                    foreach (var col in cols)
                    {
                        float colliderBottom = col.bounds.min.y;

                        if (colliderBottom < hit.point.y - 0.1f)
                            continue;

                        blocked = true;
                        break;
                    }

                    canGo[x, z] = (blocked == false);
                }
                else
                {
                    height[x, z] = NO_HEIGHT_VALUE;
                    canGo[x, z] = false;
                }
            }

            if (x % 200 == 0)
                Debug.Log($"Progress {x}/{sizeX}");
        }

        SaveBinary(height, canGo);
        Debug.Log("=== Binary Export Completed! ===");
    }

    void SaveBinary(float[,] height, bool[,] canGo)
    {
        string fileName = $"{mapDataName}.bytes";

        string localPath = Path.Combine(Application.dataPath, "Resources/Prefabs/Data/Map");
        EnsureDirectory(localPath);
        WriteBinary(Path.Combine(localPath, fileName), height, canGo);

        string externalPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Common/MapData"));
        EnsureDirectory(externalPath);
        WriteBinary(Path.Combine(externalPath, fileName), height, canGo);

        Debug.Log($"Saved Binary Map:\n→ {localPath}/{fileName}\n→ {externalPath}/{fileName}");
    }

    void WriteBinary(string path, float[,] height, bool[,] canGo)
    {
        int sx = height.GetLength(0);
        int sz = height.GetLength(1);

        using (BinaryWriter bw = new BinaryWriter(File.Open(path, FileMode.Create)))
        {
            bw.Write(cellSize);
            bw.Write(origin.x);
            bw.Write(origin.y);
            bw.Write(origin.z);
            bw.Write(sx);
            bw.Write(sz);

            for (int x = 0; x < sx; x++)
            {
                for (int z = 0; z < sz; z++)
                {
                    float h = height[x, z];
                    ushort encoded = (h < -9990) ? (ushort)0 : (ushort)((h + 100f) * 100f);
                    bw.Write(encoded);
                }
            }

            int totalCells = sx * sz;
            int byteCount = (totalCells + 7) / 8;
            byte[] packed = new byte[byteCount];

            int idx = 0;
            for (int x = 0; x < sx; x++)
            {
                for (int z = 0; z < sz; z++)
                {
                    if (canGo[x, z])
                        packed[idx >> 3] |= (byte)(1 << (idx & 7));

                    idx++;
                }
            }

            bw.Write(packed);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Map Meta 생성 (스폰 포인트 + 몬스터 스포너)
    // ──────────────────────────────────────────────────────────────
    void GenerateMeta()
    {
        if (string.IsNullOrEmpty(mapDataName))
        {
            Debug.LogError("[MapEditor] Map Data Name이 비어있습니다.");
            return;
        }

        MapMetaJson meta = new MapMetaJson();

        // EnterPoint
        MapEnterPoint enterPoint = FindObjectOfType<MapEnterPoint>();
        if (enterPoint != null)
        {
            meta.EnterPoint = ToSpawnPointJson(enterPoint.transform);
            Debug.Log($"[MapEditor] EnterPoint found: {enterPoint.transform.position}");
        }
        else
        {
            Debug.LogWarning("[MapEditor] 씬에 MapEnterPoint 컴포넌트가 없습니다.");
        }

        // LeavePoint
        MapLeavePoint leavePoint = FindObjectOfType<MapLeavePoint>();
        if (leavePoint != null)
        {
            meta.LeavePoint = ToSpawnPointJson(leavePoint.transform);
            Debug.Log($"[MapEditor] LeavePoint found: {leavePoint.transform.position}");
        }
        else
        {
            Debug.LogWarning("[MapEditor] 씬에 MapLeavePoint 컴포넌트가 없습니다.");
        }

        // MonsterSpawners
        MonsterSpawner[] spawners = FindObjectsOfType<MonsterSpawner>();
        meta.MonsterSpawners = new MonsterSpawnerJson[spawners.Length];
        for (int i = 0; i < spawners.Length; i++)
        {
            MonsterSpawner s = spawners[i];
            meta.MonsterSpawners[i] = new MonsterSpawnerJson
            {
                MonsterTypeId  = s.MonsterTypeId,
                X              = s.transform.position.x,
                Y              = s.transform.position.y,
                Z              = s.transform.position.z,
                RotY           = s.transform.eulerAngles.y,
                Count          = s.Count,
                RespawnSeconds = s.RespawnSeconds,
            };
            Debug.Log($"[MapEditor] MonsterSpawner found: TypeId={s.MonsterTypeId}, pos={s.transform.position}");
        }

        SaveMeta(meta);
    }

    SpawnPointJson ToSpawnPointJson(Transform t)
    {
        return new SpawnPointJson
        {
            X    = t.position.x,
            Y    = t.position.y,
            Z    = t.position.z,
            RotY = t.eulerAngles.y,
        };
    }

    void SaveMeta(MapMetaJson meta)
    {
        string json = JsonUtility.ToJson(meta, prettyPrint: true);
        string fileName = $"{mapDataName}_meta.json";

        string externalPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Common/MapData"));
        EnsureDirectory(externalPath);
        string fullPath = Path.Combine(externalPath, fileName);
        File.WriteAllText(fullPath, json);

        Debug.Log($"=== Map Meta Export Completed! ===\n→ {fullPath}");
    }

    void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    // ──────────────────────────────────────────────────────────────
    // JSON 직렬화용 내부 클래스 (JsonUtility 호환 - PascalCase 필드명)
    // 서버의 MapMetaData.cs와 필드명을 일치시켜 System.Text.Json이 그대로 읽을 수 있게 함
    // ──────────────────────────────────────────────────────────────
    [Serializable]
    class MapMetaJson
    {
        public SpawnPointJson    EnterPoint      = null;
        public SpawnPointJson    LeavePoint      = null;
        public MonsterSpawnerJson[] MonsterSpawners = new MonsterSpawnerJson[0];
    }

    [Serializable]
    class SpawnPointJson
    {
        public float X;
        public float Y;
        public float Z;
        public float RotY;
    }

    [Serializable]
    class MonsterSpawnerJson
    {
        public int   MonsterTypeId;
        public float X;
        public float Y;
        public float Z;
        public float RotY;
        public int   Count;
        public float RespawnSeconds;
    }
}
