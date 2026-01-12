using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Game
{
    public class LobbyManager : JobSerializer
    {
        public static LobbyManager Instance { get; } = new LobbyManager();
        
        object _lock = new object();
        Dictionary<int, Lobby> _lobbies = new Dictionary<int, Lobby>(); // key = LobbyId
        int _lobbyId = 1;
        public Dictionary<int, Lobby> Lobbies { get { return _lobbies; } }

        static List<System.Timers.Timer> _timers = new List<System.Timers.Timer>();

        // 로비 업데이트 틱 설정
        public void TickRoom(Lobby lobby, int tick = 50)
        {
            var timer = new System.Timers.Timer();
            timer.Interval = tick;
            timer.Elapsed += ((s, e) => { lobby.Update(); });
            timer.AutoReset = true;
            timer.Enabled = true;

            _timers.Add(timer);
        }

        // 클라이언트 세션을 넣는 것 보다는 Player만 넘겨도 충분하다고 판단해서 넘겨줌
        public void EnterLobby(int lobbyId, Player player)
        {
            lock (_lock)
            {
                Lobby lobby = null;
                Lobbies.TryGetValue(lobbyId, out lobby);
                if (lobby == null)
                {
                    ConsoleLogManager.Instance.Log($"Can't Find Lobby {lobbyId}");
                }
                lobby.Push(lobby.EnterLobby, player);
            }
        }

        private void MakeLobby()
        {
            lock (_lock)
            {
                Lobby newLobby = new Lobby();
                newLobby.LobbyId = _lobbyId;
                newLobby.Push(newLobby.Init);
                LobbyManager.Instance.TickRoom(newLobby);
                Lobbies.TryAdd(_lobbyId, newLobby);
                ++_lobbyId;
            }
        }

        public Lobby Find(int lobbyId)
        {
            lock (_lock)
            {
                Lobby lobby = null;
                if (_lobbies.TryGetValue(lobbyId, out lobby))
                    return lobby;

                return null;
            }
        }

        public void Init()
        {
            for (int i = 0; i < DataManager.Instance.MaxLobbyCount; ++i)
            {
                MakeLobby();
            }
            ConsoleLogManager.Instance.Log("LobbyData Donwload Complete");
        }
    }
}
