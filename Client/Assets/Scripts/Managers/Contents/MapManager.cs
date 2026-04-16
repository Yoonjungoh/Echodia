using System;
using System.IO;
using Google.Protobuf.Protocol;
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
    private GameObject _currentMapObject;
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

    // mapId: 맵 번호 (예: 1 → MapData_1.bytes)
    public void Init(int mapId = 1)
    {
        _mapData = Load(mapId);
    }

    private MapData Load(int mapId)
    {
        // Resources 폴더에서 맵 파일 로드 (MapData_{mapId}.bytes)
        TextAsset mapFile = Resources.Load<TextAsset>($"Prefabs/Data/Map/MapData_{mapId}");
        if (mapFile == null)
        {
            Managers.UI.ShowToastPopup($"맵 로딩 실패 (MapData_{mapId})\n게임을 다시 시작해주세요");
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
                    map.Height[x, z] = (encoded == 0) ? -9999f : (encoded / 100f) - 100f;
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
                    map.CanGo[x, z] = ((packed[idx >> 3] >> (idx & 7)) & 1) == 1;
                    idx++;
                }
            }
        }

        SpawnMap(mapId);

        return map;
    }

    private GameObject SpawnMap(int mapId)
    {
        if (_currentMapObject != null)
            Managers.Resource.Destroy(_currentMapObject);

        _currentMapObject = Managers.Resource.Instantiate($"Maps/Map_{mapId}");
        SpawnPortals();
        return _currentMapObject;
    }

    // ──────────────────────────────────────────────────────────────
    // 포탈 소환
    // ──────────────────────────────────────────────────────────────

    private void SpawnPortals()
    {
        MapEnterPoint enterPoint = _currentMapObject.GetComponentInChildren<MapEnterPoint>();
        if (enterPoint != null)
            SpawnPortal(enterPoint.PortalType, enterPoint.transform);
        else
            Debug.LogWarning("[MapManager] MapEnterPoint 컴포넌트를 찾을 수 없습니다.");

        MapLeavePoint leavePoint = _currentMapObject.GetComponentInChildren<MapLeavePoint>();
        if (leavePoint != null)
            SpawnPortal(leavePoint.PortalType, leavePoint.transform);
        else
            Debug.LogWarning("[MapManager] MapLeavePoint 컴포넌트를 찾을 수 없습니다.");
    }

    private void SpawnPortal(PortalType portalType, Transform point)
    {
        string path = portalType switch
        {
            PortalType.Boss => "Portals/Boss_Portal",
            _               => "Portals/Common_Portal",
        };

        Managers.Resource.Instantiate(path, point.position, point.rotation, _currentMapObject.transform);
    }

    // 맵 이동시 호출
    public void OnMapTransfer()
    {
        Managers.GameRoomObject.Clear();
        Managers.UI.ProcessOnMapTransfer();
    }
}
