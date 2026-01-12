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

        groundMask = 1 << groundLayerIndex;
        blockMask = 1 << blockLayerIndex;

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate Binary Map"))
            Generate();
    }

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

                    // ---- 원본과 동일한 Capsule 영역 ----
                    Vector3 capStart = hit.point + Vector3.up * 0.5f;
                    Vector3 capEnd = hit.point + Vector3.up * 1.5f;
                    float radius = 0.3f;

                    bool blocked = false;

                    // 🔥 2번 방법: 아래 묻힌 collider 제거
                    Collider[] cols = Physics.OverlapCapsule(capStart, capEnd, radius, blockMask);

                    foreach (var col in cols)
                    {
                        float colliderBottom = col.bounds.min.y;

                        // ⛔ 지면 아래 묻힌 collider는 무시
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
        string fileName = "MapData_001.bytes";

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

    void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }
}
