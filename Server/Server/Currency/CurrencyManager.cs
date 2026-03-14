using Google.Protobuf.Protocol;
using Microsoft.Identity.Client;
using Server.DB;
using Server.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DbTransaction = Server.DB.DbTransaction;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace Server.Currency
{
    public class CurrencyManager
    {
        public static CurrencyManager Instance { get; } = new CurrencyManager();

        // 재화 증가
        public bool AddCurrency(Player player, CurrencyType currencyType, int amount, Action callBack = null, string reason = null)
        {
            int currentCurrency = GetCurrentAmount(player.PlayerId, currencyType);
            currentCurrency += amount;
            return InternalChange(player, currencyType, currentCurrency, callBack, reason);
        }

        // 재화 감소 (소비)
        public bool SpendCurrency(Player player, CurrencyType currencyType, int cost, Action callBack = null, string reason = null)
        {
            int currentCurrency = GetCurrentAmount(player.PlayerId, currencyType);
            currentCurrency -= cost;
            return InternalChange(player, currencyType, currentCurrency, callBack, reason);
        }

        // 재화 강제 세팅
        public bool SetCurrency(Player player, CurrencyType currencyType, int amount, Action callBack = null, string reason = null)
        {
            int currentCurrency = amount;
            return InternalChange(player, currencyType, currentCurrency, callBack, reason);
        }

        // 최종적으로 모든 재화 관련 변경이 이 함수로 오게 됨
        private bool InternalChange(Player player, CurrencyType currencyType, int amount, Action callBack = null, string reason = null)
        {
            // 감소 시 잔액 부족 확인
            if (amount < 0)
            {
                // 잔액 부족 에러 처리
                ConsoleLogManager.Instance.Log
                    ($"Insufficient {currencyType}. Current: {GetCurrentAmount(player.PlayerId, currencyType)}, " +
                    $"Attempted Change: {amount}, Reason: {reason}");
                return false;
            }

            DbTransaction.SavePlayerCurrency(player, currencyType, amount, callBack);
            
            return true;
        }

        private static readonly ConcurrentDictionary<CurrencyType, Func<PlayerDb, int>> _accessorCache = new();

        public int GetCurrentAmount(int playerId, CurrencyType currencyType)
        {
            using (GameDbContext db = new GameDbContext())
            {
                PlayerDb player = db.Players
                    .AsNoTracking()
                    .FirstOrDefault(p => p.PlayerDbId == playerId);

                if (player == null)
                {
                    ConsoleLogManager.Instance.Log($"[Error] {playerId}의 {currencyType} 정보 없음");
                    return -1;
                }

                var accessor = _accessorCache.GetOrAdd(currencyType, ct =>
                {
                    var param = Expression.Parameter(typeof(PlayerDb), "p");
                    var property = Expression.Property(param, ct.ToString());
                    var lambda = Expression.Lambda<Func<PlayerDb, int>>(property, param);
                    return lambda.Compile();
                });

                return accessor(player);
            }
        }
    }
}
