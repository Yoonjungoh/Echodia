using StackExchange.Redis;

namespace Server.Session
{
    public class AccountManager
    {
        public static AccountManager Instance { get; } = new AccountManager();

        private readonly IDatabase _db;
        private const string LoginSetKey = "loggedIn";

        private AccountManager()
        {
            ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("localhost:6379");
            _db = redis.GetDatabase();
        }

        public bool IsAccountLoggedIn(int accountId)
            => _db.SetContains(LoginSetKey, accountId);

        public bool Add(int accountId)
            => _db.SetAdd(LoginSetKey, accountId);

        public bool Remove(int accountId)
            => _db.SetRemove(LoginSetKey, accountId);
    }
}
