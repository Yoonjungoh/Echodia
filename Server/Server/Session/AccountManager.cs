using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Session
{
    public class AccountManager
    {
        public static AccountManager Instance { get; } = new AccountManager();

        private HashSet<int> _loggedInAccounts = new(); // (AccountId) 로그인된 계정 ID 집합
        private object _lock = new object();

        // 이미 로그인 중인 계정인지 확인
        public bool IsAccountLoggedIn(int accountId)
        {
            lock (_lock)
            {
                return _loggedInAccounts.Contains(accountId);
            }
        }

        // 로그인된 계정 추가
        public bool Add(int accountId)
        {
            lock (_lock)
            {
                return _loggedInAccounts.Add(accountId);
            }
        }

        // 로그인된 계정 제거
        public bool Remove(int accountId)
        {
            lock (_lock)
            {
                return _loggedInAccounts.Remove(accountId);
            }
        }

    }
}
