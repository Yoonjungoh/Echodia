using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using Server.DB;
using Server.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.DB
{
    // DB 작업 해주는 클래스
    public class DbTransaction : JobSerializer
    {
        public static DbTransaction Instance { get; } = new DbTransaction();

        public static void SavePlayerCurrency(Player player, CurrencyType currencyType, int amount,  Action callBack = null, string reason = null)
        {
            if (player == null)
                return;

            Instance.Push<int, CurrencyType, int, Action, string>(SavePlayerCurrency_Db,
                player.PlayerId, currencyType, amount, callBack, reason);
        }

        private static void SavePlayerCurrency_Db(int playerId, CurrencyType currencyType, int amount, Action callBack, string reason = null)
        {
            using (GameDbContext db = new GameDbContext())
            {
                var query = db.Players
                        .Where(p => p.PlayerId == playerId);

                int successRows = currencyType switch
                {
                    // TODO - 재화 자동화 필요
                    CurrencyType.Jewel => query
                        .ExecuteUpdate(s => s.SetProperty(p => p.Jewel, amount)),

                    CurrencyType.Gold => query
                        .ExecuteUpdate(s => s.SetProperty(p => p.Gold, amount)),

                    _ => 0
                };

                if (successRows > 0)
                {
                    callBack?.Invoke();
                }
            }
        }
    }
}