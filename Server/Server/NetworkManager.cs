using Google.Protobuf.Protocol;
using Server.Game;
using System;
using System.Collections.Generic;
using System.Text;

namespace Server
{
    public class NetworkManager
    {
        public static NetworkManager Instance { get; } = new NetworkManager();

        object _lock = new object();

        public void SendTimestamp(C_Timestamp clientTimestampPacket, ClientSession clientSession)
        {
            lock (_lock)
            {
                if (clientSession == null)
                    return;

                S_Timestamp serverTimestampPacket = new S_Timestamp();
                serverTimestampPacket.ClientSendTime = clientTimestampPacket.ClientSendTime;
                serverTimestampPacket.ServerReceivedTime = Util.GetTimestampMs();
                clientSession.Send(serverTimestampPacket);
            }
        }
    }
}
