using Google.Protobuf.Protocol;
using Newtonsoft;
using Newtonsoft.Json;
using Server.Game;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Text.Json;
using static Server.Define;

namespace Server.Game
{
    // TODO - JSON 파싱
    public class DataManager
    {
        public Dictionary<RoomType, List<Vector3>> StartPositions = new Dictionary<RoomType, List<Vector3>>()
        {
            //new Vector3(-150, -18, 112),  // 숲풀
            // 평지
            //new Vector3(-676, 8, -471),
            //new Vector3(-677, 8, -471),
            //new Vector3(-678, 8, -471),
            //new Vector3(-679, 8, -471),
            // 충돌 물체
            //new Vector3(63, -26, 527),
            //new Vector3(64, -26, 527),
            //new Vector3(65, -26, 527),
            //new Vector3(66, -26, 527),
            { RoomType.WaitingRoom, new List<Vector3>()
            {
                new Vector3(-676, 8, -471),
                new Vector3(-677, 8, -471),
                new Vector3(-678, 8, -471),
                new Vector3(-679, 8, -471),
            } },
            { RoomType.GameRoom, new List<Vector3>()
            {
                // 평지 Test
                //new Vector3(63, -26, 527),
                //new Vector3(64, -26, 527),
                //new Vector3(65, -26, 527),
                //new Vector3(66, -26, 527),
                new Vector3(63, -20, 527),
                new Vector3(69, -22, 460),
                new Vector3(120, -23, 480),
                new Vector3(122, -20, 507),
            } }
        };

        public static DataManager Instance { get; } = new DataManager();
        public int MaxLobbyCount { get; set; } = 3;  // 최대 로비 개수
        public int MaxRoomPlayerCount { get; set; } = 2; // 방당 최대 플레이어 수
        public float GameStartCountdownTime { get; set; } = 3.0f; // 게임 시작 카운트다운 초기값 (클라 offset 영향 받음)
        
        public Vector3 GetStartPosition(RoomType roomType, int index)
        {
            if (index < 0 || index >= StartPositions[roomType].Count)
                return new Vector3(
                    StartPositions[roomType][StartPositions[roomType].Count - 1].X + index,
                    StartPositions[roomType][StartPositions[roomType].Count - 1].Y,
                    StartPositions[roomType][StartPositions[roomType].Count - 1].Z
                ); // 기본값

            return StartPositions[roomType][index];
        }
        public float MaxHp { get; set; } = 10000.0f;

        public float MaxDamage { get; set; } = 1000.0f;

        public float ProjectileDistanceErrorThreshold { get; set; } = 0.1f;  // 투사체 오차 허용 스레시 홀드

        public long ProcessStartTime = Util.GetTimestampMs();

        public int VictoryJewelReward { get; set; } = 100;

        public int DefaultCells { get; set; } = 200;

        // GameRoom의 GetAdjacentZones에 쓰이는 Cell 단위
        public int AdjacentZonesCells { get; set; } = 100;

        // AOIController의 GatherGameObjects에 쓰이는 Cell 단위
        public int AOICells { get; set; } = 30;
    }
}