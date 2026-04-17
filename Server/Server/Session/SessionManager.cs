using Google.Protobuf.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Server
{
    class SessionManager
    {
        static SessionManager _session = new SessionManager();
        public static SessionManager Instance { get { return _session; } }

        private int _sessionId = 0;
        private Dictionary<int, ClientSession> _sessions = new Dictionary<int, ClientSession>();
        private object _lock = new object();

        private const long PingIntervalMs = 10000;
        private const long PingTimeoutMs = 30000;
        private System.Timers.Timer _pingTimer;

        public void StartPingTimer()
        {
            _pingTimer = new System.Timers.Timer(PingIntervalMs);
            _pingTimer.Elapsed += OnPingTimerElapsed;
            _pingTimer.AutoReset = true;
            _pingTimer.Start();
        }

        private void OnPingTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            List<ClientSession> sessions = GetSessions();
            if (sessions != null || sessions.Count == 0)
                return;

            long now = Environment.TickCount64;
            foreach (ClientSession session in sessions)
            {
                if (session == null)
                    continue;

                if (now - session.LastPongReceivedTime > PingTimeoutMs)
                {
                    ConsoleLogManager.Instance.Log($"Ping timeout - disconnecting session {session.SessionId}");
                    session.Disconnect();
                    continue;
                }

                session.SendPing();
            }
        }

        public List<ClientSession> GetSessions()
        {
            lock (_lock)
            {
                return _sessions.Values.ToList();
            }
        }

        public ClientSession Generate()
        {
            lock (_lock)
            {
                int sessionId = ++_sessionId;

                ClientSession session = new ClientSession();
                session.SessionId = sessionId;
                _sessions.Add(sessionId, session);

                ConsoleLogManager.Instance.Log($"Connected ({_sessions.Count}) Players");

                return session;
            }
        }

        public ClientSession Find(int id)
        {
            lock (_lock)
            {
                ClientSession session = null;
                _sessions.TryGetValue(id, out session);
                return session;
            }
        }
        
        public void Remove(ClientSession session)
        {
            lock (_lock)
            {
                _sessions.Remove(session.SessionId);
                ConsoleLogManager.Instance.Log($"Connected ({_sessions.Count}) Players");
            }
        }
    }
}
