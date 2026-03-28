using System;
using System.Collections.Generic;
using System.Numerics;
using static Server.Define;

namespace Server
{
    public class DataManager
    {
        public static DataManager Instance { get; } = new DataManager();

        public void Init() { }

        public List<string> WorldServerNameList { get; set; } = new List<string>()
        {
            "루미나",   // 빛 Lumina
            "벨로라",   // 흐름과 세계 Velora
            "아르비안",   // 여정 Arvian
        };

        public int MaxWorldServerChannelCount { get; set; } = 5;

        public int MaxChannelPlayerCount { get; set; } = 100;

        private Dictionary<int, string> _mapNameDict { get; } = new Dictionary<int, string>()
        {
            { 1, "초원"},
            //{ 2, "숲"},
            //{ 3, "사막"},
        };

        public int MaxMapCount { get { return _mapNameDict.Count; } }

        public int MaxRoomPlayerCount { get; set; } = 2;

        public float MaxDamage { get; set; } = 1000.0f;

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
