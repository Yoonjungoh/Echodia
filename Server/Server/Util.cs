using Server.Game;
using System;
using System.Collections.Generic;
using System.Text;

namespace Server
{
    public class Util
    {
        public static long GetTimestampMs()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        }
    }
}
