using System;
using System.IO;
using UnityEngine;

public class MapData
{
    public float CellSize;
    public Vector3 Origin;
    public int SizeX;
    public int SizeZ;

    public float[,] Height;
    public bool[,] CanGo;
}

public class MapManager
{
    private MapData _mapData { get; set; }
    public static MapManager Instance { get; } = new MapManager();
    public const float NO_HEIGHT_VALUE = -9999f;

    public bool CanGo(float worldX, float worldZ)
    {
        if (_mapData == null) return false;

        int x = (int)((worldX - _mapData.Origin.x) / _mapData.CellSize);
        int z = (int)((worldZ - _mapData.Origin.z) / _mapData.CellSize);

        if (x < 0 || z < 0 || x >= _mapData.SizeX || z >= _mapData.SizeZ)
            return false;

        return _mapData.CanGo[x, z];
    }

    public float GetHeight(float worldX, float worldZ)
    {
        if (_mapData == null) return NO_HEIGHT_VALUE;

        int x = (int)((worldX - _mapData.Origin.x) / _mapData.CellSize);
        int z = (int)((worldZ - _mapData.Origin.z) / _mapData.CellSize);

        if (x < 0 || z < 0 || x >= _mapData.SizeX || z >= _mapData.SizeZ)
            return NO_HEIGHT_VALUE;

        return _mapData.Height[x, z];
    }

    public void Init()
    {
        _mapData = new MapData();
        _mapData = Load();
    }

    private MapData Load()
    {
        // Resources 폴더에서 map 파일 로드
        TextAsset mapFile = Resources.Load<TextAsset>("Prefabs/Data/Map/MapData_001");
        if (mapFile == null)
        {
            Managers.UI.ShowToastPopup("맵 로딩 실패\n게임을 다시 시작해주세요");
            return null;
        }

        MapData map = new MapData();

        using (BinaryReader br = new BinaryReader(new MemoryStream(mapFile.bytes)))
        {
            // Header
            map.CellSize = br.ReadSingle();
            float ox = br.ReadSingle();
            float oy = br.ReadSingle();
            float oz = br.ReadSingle();
            map.Origin = new Vector3(ox, oy, oz);

            map.SizeX = br.ReadInt32();
            map.SizeZ = br.ReadInt32();

            int sx = map.SizeX;
            int sz = map.SizeZ;
            int totalCells = sx * sz;

            map.Height = new float[sx, sz];
            map.CanGo = new bool[sx, sz];

            // Height (ushort)
            for (int x = 0; x < sx; x++)
            {
                for (int z = 0; z < sz; z++)
                {
                    ushort encoded = br.ReadUInt16();

                    map.Height[x, z] = (encoded == 0)
                        ? -9999f
                        : (encoded / 100f) - 100f;
                }
            }

            // Walkable (bit unpack)
            int byteCount = (totalCells + 7) / 8;
            byte[] packed = br.ReadBytes(byteCount);

            int idx = 0;
            for (int x = 0; x < sx; x++)
            {
                for (int z = 0; z < sz; z++)
                {
                    int byteIndex = idx >> 3;
                    int bitIndex = idx & 7;

                    bool walkable = ((packed[byteIndex] >> bitIndex) & 1) == 1;
                    map.CanGo[x, z] = walkable;

                    idx++;
                }
            }
        }

        return map;
    }
}