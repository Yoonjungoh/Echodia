using Google.Protobuf.Protocol;
using Server.DB;
using Server.Game;
using System;

namespace Server.Currency
{
    public class CurrencyManager
    {
        public static CurrencyManager Instance { get; } = new CurrencyManager();

        // 재화 증가
        public bool AddCurrency(Player player, CurrencyType currencyType, int amount, Action callBack = null, string reason = null)
        {
            int current = player.CurrencyTracker.Get(currencyType);
            return InternalChange(player, currencyType, current + amount, callBack, reason);
        }

        // 재화 감소 (소비)
        public bool SpendCurrency(Player player, CurrencyType currencyType, int cost, Action callBack = null, string reason = null)
        {
            int current = player.CurrencyTracker.Get(currencyType);
            return InternalChange(player, currencyType, current - cost, callBack, reason);
        }

        // 재화 강제 세팅
        public bool SetCurrency(Player player, CurrencyType currencyType, int amount, Action callBack = null, string reason = null)
        {
            return InternalChange(player, currencyType, amount, callBack, reason);
        }

        // 최종적으로 모든 재화 관련 변경이 이 함수로 오게 됨
        // 인메모리 CurrencyTracker 업데이트 + dirty 마킹 + 클라 패킷 전송
        private bool InternalChange(Player player, CurrencyType currencyType, int amount, Action callBack = null, string reason = null)
        {
            if (amount < 0)
            {
                ConsoleLogManager.Instance.Log(
                    $"Insufficient {currencyType}. Current: {player.CurrencyTracker.Get(currencyType)}, " +
                    $"Attempted Change: {amount}, Reason: {reason}");
                return false;
            }

            player.CurrencyTracker.Set(currencyType, amount);
            player.Session?.Send(new S_UpdateCurrencyData { CurrencyType = currencyType, Amount = amount });
            callBack?.Invoke();
            return true;
        }
    }
}
