using Google.Protobuf.Protocol;
using Google.Protobuf.WellKnownTypes;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Server.Game.Room;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Server.Game
{
    public class GameRoomManager : JobSerializer
    {
        private object _lock = new object();
        private Dictionary<GameRoomKey, GameRoom> _rooms = new Dictionary<GameRoomKey, GameRoom>();
        public Dictionary<GameRoomKey, GameRoom> Rooms { get { return _rooms; } }
        public int ServerId { get; set; }
        public int ChannelId { get; set; }

        public GameRoomManager(int serverId, int channelId)
        {
            ServerId = serverId;
            ChannelId = channelId;
        }

        public void Init()
        {
            lock (_lock)
            {
                // 1. 맵 개수만큼 GameRoom 생성
                int mapCount = DataManager.Instance.MaxMapCount;
                for (int mapId = 0; mapId < mapCount; ++mapId)
                {
                    CreateGameRoom(mapId);
                }
            }
        }

        public void Update()
        {
            Flush();
        }

        public void TickRoom(GameRoom room, int tick)
        {
            var sw = new System.Diagnostics.Stopwatch();

            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        // 매번 new 하지 않고 기존 sw를 0으로 만들고 다시 시작
                        sw.Restart();

                        room.Update();

                        sw.Stop();

                        // 보정값 계산
                        int delayTime = tick - (int)sw.ElapsedMilliseconds;

                        if (delayTime > 0)
                        {
                            await Task.Delay(delayTime);
                        }
                    }
                    catch (Exception ex)
                    {
                        ConsoleLogManager.Instance.Log($"[Room {room.ServerId}-{room.ChannelId} Error]: {ex.Message}");
                        await Task.Delay(tick);
                    }
                }
            });
        }

        private GameRoom CreateGameRoom(int mapId)
        {
            GameRoomKey gameRoomKey = new GameRoomKey(ServerId, ChannelId, mapId);
            if (_rooms.ContainsKey(gameRoomKey))
            {
                ConsoleLogManager.Instance.Log
                    ($"CreateGameRoom Failed! Already Exists Room ServerId:{ServerId}, ChannelId:{ChannelId}, MapId:{mapId}");
                return null;
            }

            GameRoom newRoom = new GameRoom(ServerId, ChannelId, mapId);

            newRoom.Push(newRoom.Init, DataManager.Instance.DefaultCells);
            Push(TickRoom, newRoom, 50);

            _rooms.Add(gameRoomKey, newRoom);

            return newRoom;
        }

        public GameRoom Find(int mapId)
        {
            lock (_lock)
            {
                GameRoom room = null;
                GameRoomKey gameRoomKey = new GameRoomKey(ServerId, ChannelId, mapId);
                if (_rooms.TryGetValue(gameRoomKey, out room))
                    return room;
                return null;
            }
        }

        public GameRoom Find(GameRoomKey gameRoomKey)
        {
            lock (_lock)
            {
                GameRoom room = null;
                if (_rooms.TryGetValue(gameRoomKey, out room))
                    return room;
                return null;
            }
        }
    }
}
