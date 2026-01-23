using Google.Protobuf.Protocol;
using Server.DB;
using Server.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class ServerChannel
    {
        public int ServerId { get; set; }
        public int ChannelId { get; set; }
        public int CurrentPlayerCount { get; set; }
        public int MaxPlayerCount { get; set; } = DataManager.Instance.MaxChannelPlayerCount;

        // 현재 채널에 접속 중인 유저들
        public HashSet<int> Sessions { get; set; } = new HashSet<int>();

        public GameRoomManager GameRoomManager { get; set; }

        public void Init()
        {
            GameRoomManager = new GameRoomManager(ServerId, ChannelId);
            GameRoomManager.Init();
        }

        public bool CanEnterChannel(int playerId, out EnterServerResult enterServerResult)
        {
            // 이미 접속한 플레이어인지 확인
            if (Sessions.Contains(playerId))
            {
                ConsoleLogManager.Instance.Log($"[ServerChannel] Player {playerId} is already in Channel {ChannelId} of Server {ServerId}.");
                enterServerResult = EnterServerResult.AlreadyIn;
                return false;
            }
            if (CurrentPlayerCount >= MaxPlayerCount)
            {
                enterServerResult = EnterServerResult.ChannelFull;
                return false;
            }
            if (CurrentPlayerCount < MaxPlayerCount)
            {
                enterServerResult = EnterServerResult.Success;
                return true;
            }

            // 기타 오류
            enterServerResult = EnterServerResult.InvalidServer;
            return false;
        }

        private bool EnterChannel(int sessionId)
        {
            Sessions.Add(sessionId);
            CurrentPlayerCount = Sessions.Count;
            return true;
        }

        // bool 값으로 입장 인원 꽉 찼을 때, 패킷 전송
        public bool TryEnterChannel(int sessionId, out EnterServerResult enterServerResult)
        {
            if (CanEnterChannel(sessionId, out enterServerResult))
            {
                EnterChannel(sessionId);
            }
            
            return false;
        }
        
        public bool LeaveChannel(int sessionId)
        {
            bool removeSuccess = Sessions.Remove(sessionId);
            CurrentPlayerCount = Sessions.Count;
            if (removeSuccess)
            {
                ServerManager.Instance.BroadcastChannelPlayerCountChanged(ServerId);
            }
            return removeSuccess;
        }
    }
}
