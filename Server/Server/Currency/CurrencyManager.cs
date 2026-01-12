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

namespace Server.Currency
{
    public class CurrencyManager
    {
        public static CurrencyManager Instance { get; } = new CurrencyManager();

        // 재화 증가
        public bool AddCurrency(Player player, CurrencyType currencyType, int amount, Action callBack = null, string reason = null)
        {
            if (player == null)
                return false;

            int currentCurrency = GetCurrentAmount(player, currencyType);
            currentCurrency += amount;
            return InternalChange(player, currencyType, currentCurrency, callBack, reason);
        }

        // 재화 감소 (소비)
        public bool SpendCurrency(Player player, CurrencyType currencyType, int cost, Action callBack = null, string reason = null)
        {
            if (player == null)
                return false;

            int currentCurrency = GetCurrentAmount(player, currencyType);
            currentCurrency -= cost;
            return InternalChange(player, currencyType, currentCurrency, callBack, reason);
        }
        
        // 최종적으로 모든 재화 관련 변경이 이 함수로 오게 됨
        private bool InternalChange(Player player, CurrencyType currencyType, int amount, Action callBack = null, string reason = null)
        {
            if (player == null)
                return false;

            // 감소 시 잔액 부족 확인
            if (amount < 0)
            {
                // 잔액 부족 에러 처리
                ConsoleLogManager.Instance.Log
                    ($"Insufficient {currencyType}. Current: {GetCurrentAmount(player, currencyType)}, " +
                    $"Attempted Change: {amount}, Reason: {reason}");
                return false;
            }

            DbTransaction.SavePlayerCurrency(player, currencyType, amount, callBack, reason);
            
            return true;
        }

        public int GetCurrentAmount(Player player, CurrencyType currencyType)
        {
            using (GameDbContext db = new GameDbContext())
            {
                var query = db.Players
                    .AsNoTracking()
                    .Where(p => p.PlayerId == player.PlayerId);

                // TODO - 재화 자동화 필요
                int amount = currencyType switch
                {
                    CurrencyType.Jewel => query.Select(p => p.Jewel).FirstOrDefault(),
                    CurrencyType.Gold => query.Select(p => p.Gold).FirstOrDefault(),
                    
                    _ => -1
                };

                if (amount == -1)
                {
                    ConsoleLogManager.Instance.Log($"[Error] {player.PlayerId}의 {currencyType} 정보 없음");
                    return -1;
                }

                return amount;
            }
        }
    }
}
