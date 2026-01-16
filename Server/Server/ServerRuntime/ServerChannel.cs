using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class ServerChannel
    {
        public int ChannelId { get; set; }
        public int CurrentPlayerCount { get; set; }
        public int MaxPlayerCount { get; set; } = DataManager.Instance.MaxChannelPlayerCount;
    }
}
