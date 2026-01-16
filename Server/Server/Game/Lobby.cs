using Google.Protobuf;
using Google.Protobuf.Protocol;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Timers;

namespace Server.Game
{
    public class Lobby : JobSerializer
    {
        public int LobbyId { get; set; }
        
        Dictionary<int, Player> _users = new Dictionary<int, Player>();
        public WaitingRoomManager WaitingRoomManager;
        public GameRoomManager GameRoomManager;

        public void Init()
        {
            WaitingRoomManager = new WaitingRoomManager(LobbyId);
            GameRoomManager = new GameRoomManager(LobbyId);

            WaitingRoomManager.OnRemoveRoom -= HandleRemoveRoom;
            WaitingRoomManager.OnRemoveRoom += HandleRemoveRoom;

            WaitingRoomManager.OnRoomInfoChanged -= HandleRoomInfoChanged;
            WaitingRoomManager.OnRoomInfoChanged += HandleRoomInfoChanged;

            WaitingRoomManager.OnStartGame -= HandleStartGame;
            WaitingRoomManager.OnStartGame += HandleStartGame;

            GameRoomManager.OnEmptyRoom -= HandleRemoveRoom;
            GameRoomManager.OnEmptyRoom += HandleRemoveRoom;
        }

        public void HandleAddRoom(Player user, string roomName)
        {
            if (user == null || WaitingRoomManager == null)
                return;

            // 방 생성
            WaitingRoom newRoom = WaitingRoomManager.Add(user.Id, roomName);
            
            if (newRoom == null)
            {
                ConsoleLogManager.Instance.Log($"Failed to create room: {roomName}");
                return;
            }
            
            S_AddRoom addRoomPacket = new S_AddRoom();
            addRoomPacket.RoomInfo = new RoomInfo();
            addRoomPacket.RoomInfo.RoomId = newRoom.RoomId;
            addRoomPacket.RoomInfo.RoomName = roomName;
            addRoomPacket.RoomInfo.RoomOwnerId = user.Id;
            addRoomPacket.RoomInfo.CurrentPlayerCount = 1;
            addRoomPacket.RoomInfo.MaxPlayerCount = DataManager.Instance.MaxRoomPlayerCount;
            Broadcast(addRoomPacket); 
            
            ConsoleLogManager.Instance.Log($"Room created: {newRoom.RoomId}, RoomOnwerId: {newRoom.RoomOwnerId}, RoomName: {newRoom.RoomName}");
        }

        private void HandleRoomInfoChanged(int roomId)
        {
            WaitingRoom room = null;
            WaitingRoomManager.Rooms.TryGetValue(roomId, out room);
            if (room == null)
            {
                ConsoleLogManager.Instance.Log($"Cant Find Room {roomId}");
                return;
            }

            S_UpdateWaitingRoomInfo updateWaitingRoomInfoPacket = new S_UpdateWaitingRoomInfo();
            updateWaitingRoomInfoPacket.RoomInfo = new RoomInfo();
            updateWaitingRoomInfoPacket.RoomInfo.RoomId = room.RoomId;
            updateWaitingRoomInfoPacket.RoomInfo.RoomName = room.RoomName;
            updateWaitingRoomInfoPacket.RoomInfo.RoomOwnerId = room.RoomOwnerId;
            updateWaitingRoomInfoPacket.RoomInfo.CurrentPlayerCount = room.CurrentPlayerCount;
            updateWaitingRoomInfoPacket.RoomInfo.MaxPlayerCount = DataManager.Instance.MaxRoomPlayerCount;
            Broadcast(updateWaitingRoomInfoPacket);
            ConsoleLogManager.Instance.Log($"[Manager] Room {roomId} ({updateWaitingRoomInfoPacket.RoomInfo.CurrentPlayerCount}/{updateWaitingRoomInfoPacket.RoomInfo.MaxPlayerCount})");
        }

        private void HandleRemoveRoom(int roomId)
        {
            WaitingRoom room = null;
            WaitingRoomManager.Rooms.TryGetValue(roomId, out room);
            if (room == null)
            {
                ConsoleLogManager.Instance.Log($"Cant Find Room {roomId}");
                return;
            }
            // 방의 유저들에게 나가기 알림
            WaitingRoomManager.Rooms.Remove(roomId);

            S_RemoveRoom removeRoomPacket = new S_RemoveRoom();
            removeRoomPacket.RoomId = roomId;
            Broadcast(removeRoomPacket);
            ConsoleLogManager.Instance.Log($"[Manager] Room {roomId} deleted (no players left)");
        }

        private void HandleStartGame(int roomId)
        {
            if (GameRoomManager.Rooms.ContainsKey(roomId))
            {
                ConsoleLogManager.Instance.Log($"Already Exist Room {roomId}");
                return;
            }
            // 방 정보 전달
            WaitingRoom waitingRoom = WaitingRoomManager.Find(roomId);
            if (waitingRoom == null)
            {
                ConsoleLogManager.Instance.Log($"Cant Find Room {roomId}");
                return;
            }
            GameRoomManager.Add(waitingRoom.RoomId, waitingRoom.RoomName, waitingRoom.RoomOwnerId);
        }

        public void EnterLobby(Player user)
        {
            if (user == null)
            {
                ConsoleLogManager.Instance.Log("User is Null in EnterLobby");
            }
            // 입장 처리
            if (_users.TryAdd(user.Id, user) == false)
            {
                ConsoleLogManager.Instance.Log($"Can't Add UserId: {user.Id} to Lobby {LobbyId}");
            }

            // 로비 아이디 할당
            user.Lobby = this;
            ConsoleLogManager.Instance.Log($"Lobby: Enter UserId: {user.Id}");

            // 서버에서 만든 UserId 클라 유저에게 할당
            // 혹시 할당 못 받을 경우 클라에서 재요청하게 하기 (PushAfter이랑 쓰면 패킷 처리 순서 보장 안 됨)
            S_AssignUserId s_AssignUserId = new S_AssignUserId();
            s_AssignUserId.UserId = user.Id;
            user.Session.Send(s_AssignUserId);

            // 들어온 유저에게 기존 유저들 알리고
            // 기존 유저들에게도 들어온 유저 알리기
            S_EnterLobby enterLobbyPacket = new S_EnterLobby();
            foreach (Player u in _users.Values)
            {
                if (u == null)
                    continue;

                enterLobbyPacket.UserIdList.Add(u.Id);
                enterLobbyPacket.UserNameList.Add(u.Name);
            }

            foreach (WaitingRoom room in WaitingRoomManager.Rooms.Values)
            {
                if (room == null)
                    continue;

                RoomInfo roomInfo = new RoomInfo();
                roomInfo.RoomId = room.RoomId;
                roomInfo.RoomName = room.RoomName;
                roomInfo.RoomOwnerId = room.RoomOwnerId;
                roomInfo.CurrentPlayerCount = room.CurrentPlayerCount;
                roomInfo.MaxPlayerCount = DataManager.Instance.MaxRoomPlayerCount;

                enterLobbyPacket.RoomInfoList.Add(roomInfo);
            }
            Broadcast(enterLobbyPacket);
        }

        public void LeaveLobby(int userId)
        {
            // 퇴장 처리
            if (_users.Remove(userId) == false)
            {
                ConsoleLogManager.Instance.Log($"Not Exist UserId: {userId}");
                return;
            }
            ConsoleLogManager.Instance.Log($"Lobby: Remove UserId: {userId}");

            S_LeaveLobby leaveLobbyPacket = new S_LeaveLobby();
            leaveLobbyPacket.UserId = userId;

            // 로비에 있는 유저들에게만 알리기
            foreach (Player user in _users.Values)
            {
                if (user == null) 
                    continue;

                user.Session.Send(leaveLobbyPacket);
            }
        }

        public Player Find(int userId)
        {
            Player user = null;
            _users.TryGetValue(userId, out user);
            if (user == null)
            {
                ConsoleLogManager.Instance.Log($"Can't Find UserId: {userId}");
                return null;
            }
            return user;
        }

        public Player Find(Func<GameObject, bool> condition)
        {
            foreach (Player user in _users.Values)
            {
                if (condition.Invoke(user))
                    return user;
            }

            return null;
        }

        public void Broadcast(IMessage packet)
        {
            foreach (Player user in _users.Values)
            {
                if (user == null)
                    continue;

                user.Session.Send(packet);
            }
        }

        public void Update()
        {
            Flush();
        }
    }
}