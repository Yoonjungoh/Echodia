using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DummyClient.Session
{
    public class SessionManager
    {
        public static SessionManager Instance { get; } = new SessionManager();
        
        private HashSet<ServerSession> _sessions = new HashSet<ServerSession>();

        private readonly object _lock = new object();
        private int _dummyId = 10000;   // 더미는 10000번부터 시작

        public ServerSession Generate()
        {
            lock (_lock)
            {
                ServerSession session = new ServerSession();
                session.DummyId = _dummyId++;
                
                _sessions.Add(session);
                Console.WriteLine($"Connected ({_sessions.Count}) Players");
                return session;
            }
        }

        public void Remove(ServerSession session)
        {
            lock (_lock)
            {
                _sessions.Remove(session);
                Console.WriteLine($"Disconnected ({_sessions.Count}) Players");
            }
        }
    }
}
