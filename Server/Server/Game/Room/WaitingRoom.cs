using Google.Protobuf;
using Google.Protobuf.Protocol;
using Server.Game;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Timers;
using static Server.Define;

namespace Server.Game
{
    public class WaitingRoom : JobSerializer
    {
        public int RoomId { get; set; }
        public string RoomName { get; set; }
        public int RoomOwnerId { get; set; }
        public int CurrentPlayerCount { get { return _players.Count; } }
        public bool CanEnterWaitingRoom { get { return CurrentPlayerCount < DataManager.Instance.MaxRoomPlayerCount; } }
        Dictionary<int, Player> _players = new Dictionary<int, Player>();

        public event Action<int> OnEmptyRoom; // 방이 비었을 때 알림 (roomId)
        public event Action<int> OnRoomInfoChanged;  // 방 정보 바뀌었을 때 알림 (roomId)
        public event Action<int> OnStartGame;  // 게임이 시작 됐을 때 알림 (roomId)
        public void Init()
        {
            //TestTimer();
        }

        // 어디선가 주기적으로 호출해줘야 함
        public void Update()
        {
            Flush();
        }

        public void EnterRoom(Player player)
        {
            if (player == null)
                return;

            player.WaitingRoom = this;

            S_EnterWaitingRoom enterWaitingRoomPacket = new S_EnterWaitingRoom();
            enterWaitingRoomPacket.ObjectState = new ObjectState();
            enterWaitingRoomPacket.ObjectState.Position = new ProtoVector3();
            enterWaitingRoomPacket.ObjectState.Velocity = new ProtoVector3();
            enterWaitingRoomPacket.ObjectState.Rotation = new ProtoQuaternion();
            enterWaitingRoomPacket.ObjectState.Stat = new Stat();

            // objectId 초기화
            enterWaitingRoomPacket.ObjectState.ObjectId = player.Id;

            // name 초기화
            enterWaitingRoomPacket.ObjectState.Name = player.Name;

            // position 초기화
            int spawnIndex = _players.Count % DataManager.Instance.MaxRoomPlayerCount;
            Vector3 startPos = DataManager.Instance.GetStartPosition(RoomType.WaitingRoom, spawnIndex);
            player.ObjectState.Position.X = startPos.X;
            player.ObjectState.Position.Y = startPos.Y;
            player.ObjectState.Position.Z = startPos.Z;
            enterWaitingRoomPacket.ObjectState.Position.X = player.ObjectState.Position.X;
            enterWaitingRoomPacket.ObjectState.Position.Y = player.ObjectState.Position.Y;
            enterWaitingRoomPacket.ObjectState.Position.Z = player.ObjectState.Position.Z;

            // stat 초기화
            enterWaitingRoomPacket.ObjectState.Stat = player.ObjectState.Stat;

            // creatureState 초기화
            player.ObjectState.CreatureState = CreatureState.Idle;
            enterWaitingRoomPacket.ObjectState.CreatureState = CreatureState.Idle;

            //player.Init();
            _players.Add(player.Id, player);

            player.Session.Send(enterWaitingRoomPacket);

            // 방 정보가 업데이트 된 것을 로비의 유저들에게 알려야 함
            HandleRoomInfoChanged();

            // 본인한테 맵안의 플레이어 정보 전송
            S_Spawn spawnToMePacket = new S_Spawn();

            // 나를 제외하고 접속한 플레이어를 spawnPacket에 저장
            long serverReceivedTime = Util.GetTimestampMs();
            foreach (Player p in _players.Values)
            {
                if (p == null || player == p)
                    continue;
                
                p.ObjectState.ServerReceivedTime = serverReceivedTime;
                spawnToMePacket.ObjectStateList.Add(p.ObjectState);
            }
            player.Session.Send(spawnToMePacket);

            // 다른 플레이어에게도 내가 접속한 걸 알려주기
            S_Spawn spawnToOthersPacket = new S_Spawn();
            spawnToOthersPacket.ObjectStateList.Add(player.ObjectState);
            foreach (Player p in _players.Values)
            {
                if (p.Id == player.Id)
                    continue;
                
                p.ObjectState.ServerReceivedTime = serverReceivedTime;
                p.Session.Send(spawnToOthersPacket);
                ConsoleLogManager.Instance.Log($"[WaitingRoom Update] Player {p.Id} Pos({p.Position.X}, {p.Position.Y}, {p.Position.Z})");
            }
        }

        private void HandleRoomInfoChanged()
        {
            // 해당 방 안의 유저들에게 업데이트 정보 쏴주기
            S_UpdateWaitingRoomInfo updateWaitingRoomInfoPacket = new S_UpdateWaitingRoomInfo();
            updateWaitingRoomInfoPacket.RoomInfo = new RoomInfo();
            updateWaitingRoomInfoPacket.RoomInfo.RoomId = RoomId;
            updateWaitingRoomInfoPacket.RoomInfo.RoomName = RoomName;
            updateWaitingRoomInfoPacket.RoomInfo.RoomOwnerId = RoomOwnerId;
            updateWaitingRoomInfoPacket.RoomInfo.CurrentPlayerCount = CurrentPlayerCount;
            updateWaitingRoomInfoPacket.RoomInfo.MaxPlayerCount = DataManager.Instance.MaxRoomPlayerCount;
            Broadcast(updateWaitingRoomInfoPacket);

            // 로비의 유저들에겐 아래의 액션 함수 통해서 방 인원 변경 알리기
            OnRoomInfoChanged?.Invoke(RoomId);
        }

        public void LeaveRoom(int playerId)
        {
            Player player = null;
            if (_players.Remove(playerId, out player) == false)
                return;
            
            player.WaitingRoom = null;

            // 본인에겐 다시 로비로 가라고 하기
            ExitRoom(player);

            // 타인한테 정보 전송
            {
                S_Despawn despawnPacket = new S_Despawn();
                despawnPacket.ObjectIdList.Add(playerId);
                despawnPacket.PlayerCount = _players.Count;

                foreach (Player p in _players.Values)
                {
                    if (p.Id == playerId)
                        continue;

                    p.Session.Send(despawnPacket);
                }
            }

            // 나간 사람이 방장인 경우 방 폭파
            if (RoomOwnerId == playerId)
            {
                ExitRoomAll();
                OnEmptyRoom?.Invoke(RoomId);
            }
        }

        public void ExitRoom(Player player)
        {
            if (player == null)
            {
                ConsoleLogManager.Instance.Log("Player is null");
                return;
            }
            S_ExitRoom exitRoomPacket = new S_ExitRoom();
            player.Session.Send(exitRoomPacket);

            // 방 정보가 업데이트 된 것을 로비의 유저들에게 알려야 함
            OnRoomInfoChanged?.Invoke(RoomId);
        }

        public void ExitRoomAll()
        {
            foreach (Player player in _players.Values)
            {
                ExitRoom(player);
            }
        }

        public void HandleMove(Player player, C_Move movePacket)
        {
            if (player == null || movePacket == null)
                return;

            // 서버에서 상태 업데이트
            player.ObjectState = movePacket.ObjectState;

            //TODO - 일정 거리 이상 순간이동 방지
            Vector3 serverPos = new Vector3(player.ObjectState.Position.X, player.ObjectState.Position.Y, player.ObjectState.Position.Z);
            Vector3 clientPos = new Vector3(movePacket.ObjectState.Position.X, movePacket.ObjectState.Position.Y, movePacket.ObjectState.Position.Z);

            float dist = Vector3.Distance(serverPos, clientPos);
            if (dist > 1.0f)
            {
                Console.WriteLine($"[Warning] Player {player.Id} position correction ({dist})");
                movePacket.ObjectState.Position = player.ObjectState.Position;
                movePacket.ObjectState.Velocity = new ProtoVector3 { X = 0, Y = 0, Z = 0 };
            }

            // 다른 유저들에게 브로드캐스트
            S_Move res = new S_Move { ObjectState = movePacket.ObjectState };
            res.ObjectState.ServerReceivedTime = Util.GetTimestampMs();

            Broadcast(res, player.Id);
        }

        public void StartGame(int userId, int roomId)
        {
            int userRoomId = -1;
            if (_players.TryGetValue(userId, out Player user))
            {
                if (user.WaitingRoom != null)
                {
                    userRoomId = user.WaitingRoom.RoomId;
                }
            }
            if (CanStartGame(userRoomId, roomId) == false)
            {
                ConsoleLogManager.Instance.Log($"Failed to start game. UserId: {userId}, RoomId: {roomId}");
                return;
            }
            // TODO - 대기방 로비에서 못 들어오게 처리하기
            // 만들어진 방으로 유저들을 초대
            OnStartGame?.Invoke(roomId);

            S_StartGame startGamePacket = new S_StartGame();
            foreach (int playerId in _players.Keys)
            {
                startGamePacket.PlayerIdList.Add(playerId);
            }
            Broadcast(startGamePacket);
        }

        private bool CanStartGame(int userRoomId, int roomId)
        {
            // 인원 체크, 방 번호가 요청한 사용자와 동일한지 체크
            return this.RoomId == userRoomId && this.RoomId == roomId && CurrentPlayerCount == DataManager.Instance.MaxRoomPlayerCount;
        }

        public Player FindPlayer(Func<GameObject, bool> condition)
        {
            foreach (Player player in _players.Values)
            {
                if (condition.Invoke(player))
                    return player;
            }

            return null;
        }

        public void Broadcast(IMessage packet, int? exceptId = null)
        {
            foreach (Player p in _players.Values)
            {
                if (exceptId.HasValue && p.Id == exceptId.Value)
                    continue;

                p.Session.Send(packet);
            }
        }
    }
}