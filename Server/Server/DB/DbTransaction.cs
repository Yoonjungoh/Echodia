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

        public static void UpdateQuestStatus(int playerDbId, int mainQuestId, int subQuestId, QuestStatus status, Action callback = null)
        {
            Instance.Push(UpdateQuestStatus_Db, playerDbId, mainQuestId, subQuestId, status, callback);
        }

        private static void UpdateQuestStatus_Db(int playerDbId, int mainQuestId, int subQuestId, QuestStatus status, Action callback = null)
        {
            using (GameDbContext db = new GameDbContext())
            {
                // SELECT 없이 즉시 해당 퀘스트의 Status 컬럼만 업데이트
                int successRows = db.Quests
                    .Where(q => q.PlayerDbId == playerDbId && q.MainQuestId == mainQuestId && q.SubQuestId == subQuestId)
                    .ExecuteUpdate(s => s.SetProperty(q => q.Status, q => status));

                if (successRows > 0)
                {
                    callback?.Invoke();
                }
                else
                {
                    Console.WriteLine($"[DB Error] UpdateQuestStatus Failed. Player:{playerDbId}, Quest:{mainQuestId}-{subQuestId}");
                }
            }
        }

        public static void SaveQuestComplete(QuestDb quest, Action callback = null)
        {
            Instance.Push(SaveQuestComplete_Db, quest, callback);
        }

        private static void SaveQuestComplete_Db(QuestDb quest, Action callback = null)
        {
            using (GameDbContext db = new GameDbContext())
            {
                // ExecuteUpdate를 사용하여 SELECT 없이 즉시 업데이트
                int successRows = db.Quests
                    .Where(q => q.QuestDbId == quest.QuestDbId)
                    .ExecuteUpdate(s => s
                        .SetProperty(q => q.RequiredCount, q => quest.RequiredCount)
                        .SetProperty(q => q.Status, q => quest.Status)
                        .SetProperty(q => q.ClearedDate, q => quest.ClearedDate));

                if (successRows > 0)
                {
                    callback?.Invoke();
                }
                else
                {
                    Console.WriteLine($"[DB Error] SaveQuestComplete Failed. QuestDbId: {quest.QuestDbId}");
                }
            }
        }

        public static void SavePlayerLogoutPosition(int playerDbId, float x, float y, float z, Action callback = null)
        {
            Instance.Push(SavePlayerLogoutPosition_Db, playerDbId, x, y, z, callback);
        }

        private static void SavePlayerLogoutPosition_Db(int playerDbId, float x, float y, float z, Action callback = null)
        {
            using (GameDbContext db = new GameDbContext())
            {
                var query = db.Players.Where(p => p.PlayerDbId == playerDbId);
                int successRows = query.ExecuteUpdate(s => s
                    .SetProperty(p => p.LastPosX, p => x)
                    .SetProperty(p => p.LastPosY, p => y)
                    .SetProperty(p => p.LastPosZ, p => z));

                if (successRows > 0)
                {
                    callback?.Invoke();
                }
            }
        }

        public static void SavePlayerCurrency(int playerId, CurrencyType currencyType, int amount, Action callBack = null, string reason = null)
        {
            Instance.Push(SavePlayerCurrency_Db,
                playerId, currencyType, amount, callBack, reason);
        }

        private static void SavePlayerCurrency_Db(int playerId, CurrencyType currencyType, int amount, Action callBack, string reason = null)
        {
            using (GameDbContext db = new GameDbContext())
            {
                var query = db.Players
                        .Where(p => p.PlayerDbId == playerId);

                int successRows = currencyType switch
                {
                    // TODO - 재화 자동화 필요
                    CurrencyType.Jewel => query
                        .ExecuteUpdate(s => s.SetProperty(p => p.Jewel, amount)),

                    CurrencyType.Gold => query
                        .ExecuteUpdate(s => s.SetProperty(p => p.Gold, amount)),

                    CurrencyType.Exp => query
                        .ExecuteUpdate(s => s.SetProperty(p => p.Exp, amount)),

                    CurrencyType.Level => query
                        .ExecuteUpdate(s => s.SetProperty(p => p.Level, amount)),

                    _ => 0  // default인 경우 0 반환하라는 의미
                };

                if (successRows > 0)
                {
                    callBack?.Invoke();
                }
            }
        }
    }
}