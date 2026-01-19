using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Game.Room
{
    public struct GameRoomKey : IEquatable<GameRoomKey>
    {
        public int ServerId;
        public int ChannelId;
        public int MapId;

        public GameRoomKey(int serverId, int channelId, int mapId)
        {
            ServerId = serverId;
            ChannelId = channelId;
            MapId = mapId;
        }

        public bool Equals(GameRoomKey other)
        {
            return ServerId == other.ServerId
            && ChannelId == other.ChannelId
            && MapId == other.MapId;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ServerId, ChannelId, MapId);
        }
    }
}
