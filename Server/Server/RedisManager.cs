using StackExchange.Redis;

namespace Server
{
    public class RedisManager
    {
        public static RedisManager Instance { get; } = new RedisManager();
  
        private ConnectionMultiplexer _multiplexer;
 
        private RedisManager() { }
     
        public void Init(string configuration = "localhost:6379")
        {
            _multiplexer = ConnectionMultiplexer.Connect(configuration);
        } 
     
        public IDatabase GetDatabase(int db = -1) => _multiplexer.GetDatabase(db);
    }
}
  