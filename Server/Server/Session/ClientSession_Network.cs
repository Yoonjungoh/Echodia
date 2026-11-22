using Google.Protobuf;
using Google.Protobuf.Protocol;
using Server.DB;
using Server.Game;
using Server.Session;
using ServerCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    public partial class ClientSession : PacketSession
    {
        #region 네트워크

        private List<ArraySegment<byte>> _reserveQueue = new List<ArraySegment<byte>>();

        private long _lastPongReceivedTime;
        private int _pingIdCounter = 0;

        public long LastPongReceivedTime => Interlocked.Read(ref _lastPongReceivedTime);

        public void SendPing()
        {
            S_Ping pingPacket = new S_Ping { PingId = Interlocked.Increment(ref _pingIdCounter) };
            Send(pingPacket);
            FlushSend();
        }

        // 예약만 하고 실질적으로 보내진 않음
        public void Send(IMessage packet)
        {
            string msgName = packet.Descriptor.Name.Replace("_", string.Empty);
            MsgId msgId = (MsgId)Enum.Parse(typeof(MsgId), msgName);
            ushort size = (ushort)packet.CalculateSize();
            byte[] sendBuffer = new byte[size + 4];
            Array.Copy(BitConverter.GetBytes((ushort)(size + 4)), 0, sendBuffer, 0, sizeof(ushort));
            Array.Copy(BitConverter.GetBytes((ushort)msgId), 0, sendBuffer, 2, sizeof(ushort));
            Array.Copy(packet.ToByteArray(), 0, sendBuffer, 4, size);

            lock (_lock)
            {
                _reserveQueue.Add(sendBuffer);
            }
            //Send(new ArraySegment<byte>(sendBuffer));
        }

        // 실제로 보내는 부분
        public void FlushSend()
        {
            List<ArraySegment<byte>> sendList = null;

            lock (_lock)
            {
                if (_reserveQueue.Count == 0)
                    return;

                sendList = _reserveQueue;
                _reserveQueue = new List<ArraySegment<byte>>();
            }

            Send(sendList);
        }

        public override void OnConnected(EndPoint endPoint)
        {
            ConsoleLogManager.Instance.Log($"OnConnected : {endPoint}");

            Interlocked.Exchange(ref _lastPongReceivedTime, Environment.TickCount64);

            Send(new S_Connected());
         }

        public void OnPongReceived()
        {
            Interlocked.Exchange(ref _lastPongReceivedTime, Environment.TickCount64);
        }

        public override void OnRecvPacket(ArraySegment<byte> buffer)
        {
            PacketManager.Instance.OnRecvPacket(this, buffer);
        }

        public override void OnDisconnected(EndPoint endPoint)
        {
            // Serialize with the login commit if the socket closes during verification.
            GameRoom logoutRoom;
            Player player;
            int loggedInAccount;
            lock (_lock)
            {
                if (_loginClosed) return;
                _loginClosed = true;
                loggedInAccount = ClientServerState == ClientServerState.Login ? 0 : AccountId;
                player = MyPlayer;
                logoutRoom = player?.GameRoom ?? _pendingEntryRoom;
            }
            if (ChannelId != 0) ServerManager.Instance.Remove(SessionId);
            SessionManager.Instance.Remove(this);
            if (loggedInAccount == 0) return;
            if (player == null || logoutRoom == null)
            {
                AccountManager.Instance.Remove(loggedInAccount);
                return;
            }
            // Keep the login reservation until the old character's last economic
            // checkpoint is durable. During recovery a reconnect can retry login.
            void LeaveAndRelease(GameRoom room)
            {
                GameRoom current;
                lock (_lock) current = player.GameRoom ?? _pendingEntryRoom;
                if (current != null && current != room)
                {
                    current.Push(() => LeaveAndRelease(current));
                    return;
                }
                if (player.GameRoom == room)
                {
                    if (player.Economy.PrepareLeave(player, () => LeaveAndRelease(room))) return;
                    DbTransaction.SavePlayerLogoutState(player.PlayerId, player.CurrentPosition, room.MapId);
                    room.LeaveGame(player.Id);
                }
                DbTransaction.Instance.Push(() => AccountManager.Instance.Remove(loggedInAccount));
            }
            logoutRoom.Push(() => LeaveAndRelease(logoutRoom));
        }

        public override void OnSend(int numOfBytes)
        {
            //ConsoleLogManager.Instance.Log($"Transferred bytes: {numOfBytes}");
        }
        #endregion
    }
}
