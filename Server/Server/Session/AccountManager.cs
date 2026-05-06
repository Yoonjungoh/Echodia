namespace Server.Session
{
    public class AccountManager
    {
        public static AccountManager Instance { get; } = new AccountManager();

        private const string LoginSetKey = "loggedIn";

        private AccountManager() { }

        public bool IsAccountLoggedIn(int accountId)
            => RedisManager.Instance.GetDatabase().SetContains(LoginSetKey, accountId);

        public bool Add(int accountId)
            => RedisManager.Instance.GetDatabase().SetAdd(LoginSetKey, accountId);

        public bool Remove(int accountId)
            => RedisManager.Instance.GetDatabase().SetRemove(LoginSetKey, accountId);
    }
}
