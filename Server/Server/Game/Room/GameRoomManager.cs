using Google.Protobuf.Protocol;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Game
{
    public class GameRoomManager : JobSerializer
    {
        object _lock = new object();
        Dictionary<int, GameRoom> _rooms = new Dictionary<int, GameRoom>();
        public Dictionary<int, GameRoom> Rooms { get { return _rooms; } }
        List<System.Timers.Timer> _timers = new List<System.Timers.Timer>();
        public event Action<int> OnEmptyRoom; // 방이 비었을 때 알림 (roomId)
        public GameRoomManager(int lobbyId)
        {
            ConsoleLogManager.Instance.Log($"GameRoomManager created for Lobby {lobbyId}");
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

        public GameRoom Add(int roomId, string roomName, int roomOwnerId)
        {
            lock (_lock)
            {
                GameRoom newRoom = new GameRoom();
                newRoom.Push(newRoom.Init, DataManager.Instance.DefaultCells);
                TickRoom(newRoom);

                if (_rooms.ContainsKey(roomId))
                {
                    ConsoleLogManager.Instance.Log($"That room already exist {roomId}");
                    return null;
                }

                newRoom.RoomId = roomId;
                newRoom.RoomName = roomName;
                newRoom.RoomOwnerId = roomOwnerId;

                newRoom.OnEmptyRoom -= HandleEmptyRoom;
                newRoom.OnEmptyRoom += HandleEmptyRoom;

                _rooms.Add(roomId, newRoom);

                return newRoom;
            }
        }

        private void HandleEmptyRoom(int roomId)
        {
            if (_rooms.ContainsKey(roomId) == false)
            {
                ConsoleLogManager.Instance.Log($"Cant Find Room {roomId}");
                return;
            }
            OnEmptyRoom?.Invoke(roomId);
        }

        public bool Remove(int roomId)
        {
            lock (_lock)
            {
                if (_rooms.ContainsKey(roomId))
                    return _rooms.Remove(roomId);
                else
                {
                    ConsoleLogManager.Instance.Log($"That room already removed {roomId}");
                    return false;
                }
            }
        }

        public GameRoom Find(int roomId)
        {
            lock (_lock)
            {
                GameRoom room = null;
                if (_rooms.TryGetValue(roomId, out room))
                    return room;
                return null;
            }
        }

        public void DisposeTimer()
        {
            foreach (var timer in _timers)
            {
                timer.Stop();
                timer.Dispose();
            }
            _timers.Clear();
        }
    }
}
