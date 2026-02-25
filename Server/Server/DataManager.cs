using Google.Protobuf.Protocol;
using Newtonsoft;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Text.Json;
using static Server.Define;

namespace Server
{
    // TODO - JSON 파싱
    public class DataManager
    {
        public static DataManager Instance { get; } = new DataManager();
        // 레벨업에 필요한 경험치 테이블 만들어야 함
        private static readonly Dictionary<int, int> _expDictionary = new Dictionary<int, int>()
        {
            { 1, 600 },
            { 2, 1000 },
            { 3, 3000 },
            { 4, 6000 },
            { 5, 10000 },
            // TODO - 나머지 레벨업 경험치 테이블 채우기
        };

        public int GetExpForLevelUp(int level)
        {
            if (_expDictionary.ContainsKey(level))
                return _expDictionary[level];
            
            return int.MaxValue; // TODO - 레벨업 불가능한 최대 경험치 반환
        }

        public Vector3 StartPositions = new Vector3(63, -20, 527);
       
        public List<string> WorldServerNameList { get; set; } = new List<string>()
        {
            "루미나",   // 빛 Lumina
            "벨로라",   // 흐름과 세계 Velora
            "아르비안",   // 여정 Arvian
        };

        public int MaxWorldServerChannelCount { get; set; } = 5; // 최대 월드의 서버 채널 개수
        
        public int MaxChannelPlayerCount { get; set; } = 100;   // 채널에 들어갈 수 있는 최대 플레이어 수

        private Dictionary<int, string> _mapNameDict { get; } = new Dictionary<int, string>()
        {
            { 1, "초원"},
            //{ 2, "숲"},
            //{ 3, "사막"},
        };

        public int MaxMapCount { get { return _mapNameDict.Count; } }   // 맵 최대 개수

        public int MaxLobbyCount { get; set; } = 3;  // 최대 로비 개수

        public int MaxRoomPlayerCount { get; set; } = 2; // 방당 최대 플레이어 수

        public float GameStartCountdownTime { get; set; } = 3.0f; // 게임 시작 카운트다운 초기값 (클라 offset 영향 받음)
        
        // 인게임 최대 체력
        public float MaxHp { get; set; } = 10000.0f;

        public float MaxDamage { get; set; } = 1000.0f;

        public float ProjectileDistanceErrorThreshold { get; set; } = 0.1f;  // 투사체 오차 허용 스레시 홀드

        public long ProcessStartTime = Util.GetTimestampMs();

        public int VictoryJewelReward { get; set; } = 100;

        public int DefaultCells { get; set; } = 200;

        // GameRoom의 GetAdjacentZones에 쓰이는 Cell 단위
        public int AdjacentZonesCells { get; set; } = 100;

        // AOIController의 GatherGameObjects에 쓰이는 Cell 단위
        public int AOICells { get; set; } = 200; // 디폴트 50 추천

        private Random _rand = new Random();
        public Vector3 GetStartPosition()
        {
            // TODO - 봇테스트를 위한 랜덤 위치 값
            float randomX = (float)(_rand.NextDouble() * 20 - 5 + 63);
            float fixedY = -20;
            float randomZ = (float)(_rand.NextDouble() * 20 - 5 + 527);
            return new Vector3(randomX, fixedY, randomZ);
            //return StartPositions;
        }

        public string GetMapName(int mapId)
        {
            if (_mapNameDict.ContainsKey(mapId))
                return _mapNameDict[mapId];

            return "None";
        }
    }
}