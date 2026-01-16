using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class WorldServer
    {
        public int ServerId { get; set; }    // 내부 식별자 (불변)
        public string Name { get; set; } // UI 표시용 서버 이름
        public Dictionary<int, ServerChannel> Channels { get; set; } = new Dictionary<int, ServerChannel>();
    }
}
