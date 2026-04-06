using System;
using System.Collections.Generic;
using System.Numerics;
using Server.Data;
using static Server.Define;

namespace Server
{
    public class DataManager
    {
        public static DataManager Instance { get; } = new DataManager();

        public void Init()
        {
            DefaultStartMapId = ConfigManager.Instance.GetInt(ConfigType.DefaultStartMapId);
        }

        public List<string> WorldServerNameList { get; set; } = new List<string>()
        {
            "루미나",   // 빛 Lumina
            "벨로라",   // 흐름과 세계 Velora
            "아르비안",   // 여정 Arvian
        };

        public int MaxWorldServerChannelCount { get; set; } = 5;

        public int MaxChannelPlayerCount { get; set; } = 100;

        // mapId -> 맵 표시 이름
        private Dictionary<int, string> _mapNameDict { get; } = new Dictionary<int, string>()
        {
            { 1, "사막" },
            { 2, "던전" },
        };

        public int MaxMapCount { get { return _mapNameDict.Count; } }

        // 모든 맵 Id 목록
        public IEnumerable<int> MapIds => _mapNameDict.Keys;

        // 플레이어가 처음 입장하는 맵 Id
        public int DefaultStartMapId { get; private set; }

        // 맵 연결 그래프: mapId -> (이전 맵 Id, 다음 맵 Id), -1 = 없음
        // EnterPoint 근처에서 이동 -> 이전 맵(Prev)으로, 이전 맵의 LeavePoint에 스폰
        // LeavePoint 근처에서 이동 -> 다음 맵(Next)으로, 다음 맵의 EnterPoint에 스폰
        private Dictionary<int, (int Prev, int Next)> _mapGraph { get; } = new Dictionary<int, (int, int)>()
        {
            { 1, (-1, 2) },
            { 2, (1, -1) },
        };

        public (int Prev, int Next) GetAdjacentMaps(int mapId)
        {
            if (_mapGraph.TryGetValue(mapId, out var adj))
                return adj;

            return (-1, -1);
        }

        public int MaxRoomPlayerCount { get; set; } = 2;

        public int MaxDamage { get; set; } = 1000;

        public float ProjectileDistanceErrorThreshold { get; set; } = 0.1f;

        public int DefaultCells { get; set; } = 200;

        public int AdjacentZonesCells { get; set; } = 100;

        public int AOICells { get; set; } = 200;

        private Random _rand = new Random();

        public Vector3 GetStartPosition()
        {
            float randomX = (float)(_rand.NextDouble() * 20 - 5 + 63);
            float fixedY = -20;
            float randomZ = (float)(_rand.NextDouble() * 20 - 5 + 527);
            return new Vector3(randomX, fixedY, randomZ);
        }

        public string GetMapName(int mapId)
        {
            if (_mapNameDict.ContainsKey(mapId))
                return _mapNameDict[mapId];

            return "None";
        }
    }
}
