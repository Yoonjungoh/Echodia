using Google.Protobuf.Protocol;
using Google.Protobuf.WellKnownTypes;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Server.Game.Room;
using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Game
{
    public class GameRoomManager : JobSerializer
    {
        private object _lock = new object();
        private Dictionary<GameRoomKey, GameRoom> _rooms = new Dictionary<GameRoomKey, GameRoom>();
        public Dictionary<GameRoomKey, GameRoom> Rooms { get { return _rooms; } }
        private List<System.Timers.Timer> _timers = new List<System.Timers.Timer>();
        public int ServerId { get; set; }
        public int ChannelId { get; set; }

        public GameRoomManager(int serverId, int channelId)
        {
            ServerId = serverId;
            ChannelId = channelId;
        }

        public void Init()
        {
            // 1. 맵 개수만큼 GameRoom 생성
            int mapCount = DataManager.Instance.MaxMapCount;
            for (int mapId = 0; mapId < mapCount; ++mapId)
            {
                CreateGameRoom(1, 1, mapId);
            }
        }

        public void TickRoom(GameRoom room, int tick = 50)
        {
            var timer = new System.Timers.Timer();
            timer.Interval = tick;
            timer.Elapsed += ((s, e) => { room.Update(); });
            timer.AutoReset = true;
            timer.Enabled = true;
            _timers.Add(timer);
        }

        private GameRoom CreateGameRoom(int serverId, int channelId, int mapId)
        {
            lock (_lock)
            {
                GameRoomKey gameRoomKey = new GameRoomKey(serverId, channelId, mapId);
                if (_rooms.ContainsKey(gameRoomKey))
                {
                    ConsoleLogManager.Instance.Log
                        ($"CreateGameRoom Failed! Already Exists Room ServerId:{serverId}, ChannelId:{channelId}, MapId:{mapId}");
                    return null;
                }

                GameRoom newRoom = new GameRoom(serverId, channelId, mapId);

                newRoom.Push(newRoom.Init, DataManager.Instance.DefaultCells);
                TickRoom(newRoom);

                _rooms.Add(gameRoomKey, newRoom);

                return newRoom;
            }
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
