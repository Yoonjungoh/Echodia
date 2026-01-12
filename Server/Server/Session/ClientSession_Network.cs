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

            S_Connected connectedPacket = new S_Connected();
            Send(connectedPacket);
        }

        public override void OnRecvPacket(ArraySegment<byte> buffer)
        {
            PacketManager.Instance.OnRecvPacket(this, buffer);
        }

        public override void OnDisconnected(EndPoint endPoint)
        {
            if (MyPlayer == null)
            {
                ConsoleLogManager.Instance.Log("Can't Find MyPlayer");
                AccountManager.Instance.Remove(AccountId);
                return;
            }

            // 로비에서 내보내기
            if (MyPlayer.Lobby != null)
            {
                MyPlayer.Lobby.Push(MyPlayer.Lobby.LeaveLobby, MyPlayer.Id);
            }

            // 대기방에서 내보내기
            if (MyPlayer.WaitingRoom != null)
            {
                WaitingRoom watingRoom = MyPlayer.Lobby.WaitingRoomManager.Find(MyPlayer.WaitingRoom.RoomId);
                if (watingRoom != null)
                {
                    watingRoom.Push(watingRoom.LeaveRoom, MyPlayer.ObjectState.ObjectId);
                }
                else
                {
                    ConsoleLogManager.Instance.Log($"Can't Find Room {MyPlayer.WaitingRoom.RoomId} -> UserId: {MyPlayer.Id}");
                }
            }

            AccountManager.Instance.Remove(AccountId);
            SessionManager.Instance.Remove(this);

            ConsoleLogManager.Instance.Log($"OnDisconnected : {endPoint}");
        }

        public override void OnSend(int numOfBytes)
        {
            //ConsoleLogManager.Instance.Log($"Transferred bytes: {numOfBytes}");
        }

        public void EnterLobby()
        {
            ConsoleLogManager.Instance.Log($"Player Connected in Lobby {MyPlayer.Session.SessionId}");
            LobbyManager.Instance.EnterLobby(1, MyPlayer);	// TODO - 1번 로비로 강제 이동
        }
        #endregion
    }
}
