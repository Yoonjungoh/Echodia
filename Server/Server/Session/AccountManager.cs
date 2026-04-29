namespace Server.Session
{
    public class AccountManager
    {
        public static AccountManager Instance { get; } = new AccountManager();

        private const string LoginSetKey = "loggedIn";

        private AccountManager() { }

        public static bool IsAccountLoggedIn(int accountId)
            => RedisManager.Instance.GetDatabase().SetContains(LoginSetKey, accountId);

        public static bool Add(int accountId)
            => RedisManager.Instance.GetDatabase().SetAdd(LoginSetKey, accountId);

        public static bool Remove(int accountId)
            => RedisManager.Instance.GetDatabase().SetRemove(LoginSetKey, accountId);
    }
}
