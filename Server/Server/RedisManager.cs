using StackExchange.Redis;
using System;
using System.Linq;
using System.Net;
using Server.Configuration;

namespace Server
{
    public class RedisManager
    {
        public static RedisManager Instance { get; } = new RedisManager();
  
        private ConnectionMultiplexer _multiplexer;
 
        private RedisManager() { }
      
        public void Init(string configuration = null)
        {
            configuration ??= ReadConfiguration();
            _multiplexer = ConnectionMultiplexer.Connect(ParseConfiguration(configuration));
        } 

        private static string ReadConfiguration()
        {
            string protectedPath = Environment.GetEnvironmentVariable("ECHODIA_REDIS_CONNECTION_DPAPI_FILE");
            string clear = Environment.GetEnvironmentVariable("ECHODIA_REDIS_CONNECTION");
            if (string.IsNullOrWhiteSpace(protectedPath))
                return clear ?? "localhost:6379";
            if (!string.IsNullOrWhiteSpace(clear))
                throw new InvalidOperationException("Set only one source for the Redis credential.");
            return MachineProtectedSecret.Read(protectedPath);
        }

        internal static ConfigurationOptions ParseConfiguration(string configuration)
        {
            var options = ConfigurationOptions.Parse(configuration);
            if (options.EndPoints.Count == 0)
                throw new InvalidOperationException("A Redis endpoint is required.");
            if (options.EndPoints.Any(endpoint => !IsLoopback(endpoint)) &&
                (!options.Ssl || string.IsNullOrWhiteSpace(options.Password)))
                throw new InvalidOperationException("Remote Redis requires TLS and an authentication credential.");
            return options;
        }

        private static bool IsLoopback(EndPoint endpoint) => endpoint switch
        {
            IPEndPoint ip => IPAddress.IsLoopback(ip.Address),
            DnsEndPoint dns => string.Equals(dns.Host, "localhost", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
      
        public IDatabase GetDatabase(int db = -1) => _multiplexer.GetDatabase(db);
    }
}
