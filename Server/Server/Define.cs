using System;
using System.Collections.Generic;
using System.Text;

namespace Server
{
    public static class Define
    {
        public enum RoomType
        {
            None = 0,
            Lobby = 1,
            WaitingRoom = 1,
            GameRoom = 2,
            Error = 4,
        }
    }
}
