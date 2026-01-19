using Google.Protobuf.Collections;
using Google.Protobuf.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class ServerManager
    {
        public static ServerManager Instance { get; } = new ServerManager();

        private int _serverId = 1; // 서버의 UId 설정

        // ServerId -> WorldServer
        public Dictionary<int, WorldServer> WorldServers { get; set; } = new Dictionary<int, WorldServer>();

        // 서버 Id와 이름만 반환 (클라이언트에서 서버 표시 용도)
        public List<ServerInfo> ServerSummaryList { get; set; } = new List<ServerInfo>();
        
        public void Init()
        {
            CreateWorldServers();
        }

        public ServerChannel FindChannel(int serverId, int channelId)
        {
            if (WorldServers.TryGetValue(serverId, out WorldServer worldServer))
            {
                if (worldServer.Channels.TryGetValue(channelId, out ServerChannel channel))
                {
                    return channel;
                }
            }
            
            return null;
        }

        // 월드 서버 생성 로직
        private void CreateWorldServers()
        {
            foreach (string worldServerName in DataManager.Instance.WorldServerNameList)
            {
                WorldServer newWorldServer = new WorldServer
                {
                    ServerId = _serverId,
                    Name = worldServerName,
                    Channels = new Dictionary<int, ServerChannel>()
                };

                WorldServers.Add(newWorldServer.ServerId, newWorldServer);
                ServerSummaryList.Add(new ServerInfo
                {
                    ServerId = newWorldServer.ServerId,
                    ServerName = newWorldServer.Name
                });

                int channelCount = DataManager.Instance.MaxWorldServerChannelCount;
                int maxChannelPlayerCount = DataManager.Instance.MaxChannelPlayerCount;

                for (int i = 1; i <= channelCount; ++i)
                {
                    ServerChannel channel = new ServerChannel
                    {
                        ServerId = newWorldServer.ServerId,
                        ChannelId = i,
                        MaxPlayerCount = maxChannelPlayerCount
                    };
                    channel.Init();
                    newWorldServer.Channels.Add(i, channel);
                }

                ++_serverId;
            }
        }

        // ClientSessin_PreGame.cs의 HandleRequestServerList 함수에 의해서 
        // WorldServer를 ServerInfo 포멧으로 만들어야 함
        public List<ServerInfo> GetServerInfoList(int serverId)
        {
            List<ServerInfo> serverInfoList = new List<ServerInfo>();
            if (WorldServers.TryGetValue(serverId, out WorldServer worldServer))
            {
                foreach (ServerChannel channel in worldServer.Channels.Values)
                {
                    ServerInfo serverInfo = new ServerInfo
                    {
                        ServerId = worldServer.ServerId,
                        ServerName = worldServer.Name,
                        ChannelId = channel.ChannelId,
                        CurrentPlayerCount = channel.CurrentPlayerCount,
                        MaxPlayerCount = channel.MaxPlayerCount
                    };
                    
                    serverInfoList.Add(serverInfo);
                }
            }

            return serverInfoList;
        }

        public bool IsValidServerChannel(int serverId, int channel)
        {
            if (WorldServers.TryGetValue(serverId, out WorldServer worldServer))
            {
                return worldServer.Channels.ContainsKey(channel);
            }
            
            return false;
        }
    }

}
