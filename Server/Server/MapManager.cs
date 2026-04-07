using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;

namespace Server
{
    public class MapData
    {
        public int MapId { get; set; } = -1;
        public float CellSize { get; set; }
        public Vector3 Origin { get; set; }
        public int SizeX { get; set; }
        public int SizeZ { get; set; }
        public float[,] Height { get; set; }
        public bool[,] CanGo { get; set; }

        // 스폰 포인트 (메타 JSON에서 로드, 없으면 null)
        public SpawnPointData EnterPoint { get; set; }
        public SpawnPointData LeavePoint { get; set; }

        // 몬스터 스포너 목록 (메타 JSON에서 로드)
        public List<MonsterSpawnerData> MonsterSpawners { get; set; } = new();

        public int MinX { get { return -(SizeX / 2); } }
        public int MaxX { get { return (SizeX / 2); } }
        public int MinZ { get { return -(SizeZ / 2); } }
        public int MaxZ { get { return (SizeZ / 2); } }

        public MapData(int mapId)
        {
            MapId = mapId;
        }
    }

    public class MapManager
    {
        // 템플릿 맵 (Zone 없는 순수 지형 데이터) - GameRoom.Init()에서 Clone해 사용
        private Dictionary<int, MapData> _maps = new Dictionary<int, MapData>();

        public static MapManager Instance { get; } = new MapManager();

        // 루트 경로 (exe → Common/MapData 폴더)
        private string _rootPath;

        public void Init()
        {
            string exeDir = AppContext.BaseDirectory;
            _rootPath = Path.GetFullPath(Path.Combine(exeDir, @"..\..\..\..\..\"));

            foreach (int mapId in DataManager.Instance.MapIds)
            {
                string path = GetMapPath(mapId);
                if (!File.Exists(path))
                {
                    Console.WriteLine($"[MapManager] Map file not found: {path}");
                    continue;
                }

                MapData map = Load(mapId, path);

                string metaPath = GetMetaPath(mapId);
                LoadMeta(map, metaPath);

                _maps[mapId] = map;
                Console.WriteLine($"[MapManager] Loaded map {mapId}: {map.SizeX} x {map.SizeZ}");
            }
        }

        public MapData CreateCopy(int mapId)
        {
            if (!_maps.TryGetValue(mapId, out MapData template))
            {
                Console.WriteLine($"[MapManager] CreateCopy failed — mapId {mapId} not loaded.");
                return null;
            }

            MapData copy = new MapData(mapId);
            copy.CellSize = template.CellSize;
            copy.Origin   = template.Origin;
            copy.SizeX    = template.SizeX;
            copy.SizeZ    = template.SizeZ;

            copy.Height = (float[,])template.Height.Clone();
            copy.CanGo  = (bool[,])template.CanGo.Clone();

            copy.EnterPoint      = template.EnterPoint;
            copy.LeavePoint      = template.LeavePoint;
            copy.MonsterSpawners = template.MonsterSpawners;

            return copy;
        }

        // 경로 헬퍼
        // MapData_{mapId}.bytes  (예: MapData_1.bytes, MapData_12.bytes)
        private string GetMapPath(int mapId)
            => Path.Combine(_rootPath, "Common", "MapData", $"MapData_{mapId}.bytes");

        // MapData_{mapId}_meta.json
        private string GetMetaPath(int mapId)
            => Path.Combine(_rootPath, "Common", "MapData", $"MapData_{mapId}_meta.json");

        // 메타 로드
        private void LoadMeta(MapData map, string metaPath)
        {
            if (!File.Exists(metaPath))
            {
                Console.WriteLine($"[MapManager] Meta file not found, skipping: {metaPath}");
                return;
            }

            try
            {
                string json = File.ReadAllText(metaPath);
                JsonSerializerOptions options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                };
                MapMetaData meta = JsonSerializer.Deserialize<MapMetaData>(json, options);
                if (meta == null)
                    return;

                map.EnterPoint      = meta.EnterPoint;
                map.LeavePoint      = meta.LeavePoint;
                map.MonsterSpawners = meta.MonsterSpawners ?? new();

                Console.WriteLine($"[MapManager] Meta loaded for map {map.MapId}: " +
                    $"EnterPoint={map.EnterPoint?.Position}, " +
                    $"LeavePoint={map.LeavePoint?.Position}, " +
                    $"Spawners={map.MonsterSpawners.Count}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[MapManager] Failed to load meta for map {map.MapId}: {e.Message}");
            }
        }

        // 바이너리 로드
        private MapData Load(int mapId, string filePath)
        {
            MapData map = new MapData(mapId);

            using (BinaryReader br = new BinaryReader(File.OpenRead(filePath)))
            {
                map.CellSize = br.ReadSingle();
                float ox = br.ReadSingle();
                float oy = br.ReadSingle();
                float oz = br.ReadSingle();
                map.Origin = new Vector3(ox, oy, oz);

                map.SizeX = br.ReadInt32();
                map.SizeZ = br.ReadInt32();

                int sx = map.SizeX;
                int sz = map.SizeZ;
                int total = sx * sz;

                map.Height = new float[sx, sz];
                map.CanGo  = new bool[sx, sz];

                // Height 디코딩 (ushort → float)
                for (int x = 0; x < sx; x++)
                    for (int z = 0; z < sz; z++)
                    {
                        ushort encoded = br.ReadUInt16();
                        map.Height[x, z] = (encoded == 0) ? -9999f : encoded / 100f - 100f;
                    }

                // CanGo 비트 언패킹
                int byteCount = (total + 7) / 8;
                byte[] packed = br.ReadBytes(byteCount);

                int idx = 0;
                for (int x = 0; x < sx; x++)
                    for (int z = 0; z < sz; z++)
                    {
                        map.CanGo[x, z] = ((packed[idx >> 3] >> (idx & 7)) & 1) == 1;
                        idx++;
                    }
            }

            return map;
        }
    }
}
