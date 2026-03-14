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

        // 퀘스트 보상 수령: Completed 상태 확인 → RewardClaimed 변경 → 재화 증가 → 새 재화값 콜백
        public static void GiveQuestReward(int playerDbId, int mainQuestId, int subQuestId,
            int rewardAmount, CurrencyType currencyType, Action<int> onSuccess = null)
        {
            // TODO - 파라미터 6개로 Push 오버로드 초과 -> 클로저 캡처
            Instance.Push(() => GiveQuestReward_Db(playerDbId, mainQuestId, subQuestId, rewardAmount, currencyType, onSuccess));
        }

        private static void GiveQuestReward_Db(int playerDbId, int mainQuestId, int subQuestId,
            int rewardAmount, CurrencyType currencyType, Action<int> onSuccess)
        {
            using GameDbContext db = new GameDbContext();
            using var transaction = db.Database.BeginTransaction();
            try
            {
                // 1. Completed 상태인 퀘스트를 RewardClaimed로 변경
                int questRows = db.Quests
                    .Where(q => q.PlayerDbId == playerDbId
                             && q.MainQuestId == mainQuestId
                             && q.SubQuestId == subQuestId
                             && q.Status == QuestStatus.Completed)
                    .ExecuteUpdate(s => s.SetProperty(q => q.Status, QuestStatus.RewardClaimed));

                if (questRows == 0)
                {
                    Console.WriteLine($"[DB Error] GiveQuestReward: 퀘스트가 Completed 상태가 아닙니다. Player:{playerDbId}, Quest:{mainQuestId}-{subQuestId}");
                    transaction.Rollback();
                    return;
                }

                // 2. 재화 증가
                var playerQuery = db.Players.Where(p => p.PlayerDbId == playerDbId);
                int successRows = currencyType switch
                {
                    CurrencyType.Gold => playerQuery.ExecuteUpdate(s => s.SetProperty(p => p.Gold, p => p.Gold + rewardAmount)),
                    CurrencyType.Jewel => playerQuery.ExecuteUpdate(s => s.SetProperty(p => p.Jewel, p => p.Jewel + rewardAmount)),
                    CurrencyType.Exp => playerQuery.ExecuteUpdate(s => s.SetProperty(p => p.Exp, p => p.Exp + rewardAmount)),
                    _ => 0
                };

                if (successRows == 0)
                {
                    Console.WriteLine($"[DB Error] GiveQuestReward: 재화 업데이트 실패. Player:{playerDbId}, Currency:{currencyType}");
                    transaction.Rollback();
                    return;
                }

                // 3. 새 재화값 읽기
                PlayerDb playerDb = db.Players.AsNoTracking()
                    .Where(p => p.PlayerDbId == playerDbId)
                    .FirstOrDefault();

                transaction.Commit();

                int newAmount = currencyType switch
                {
                    CurrencyType.Gold => playerDb?.Gold ?? 0,
                    CurrencyType.Jewel => playerDb?.Jewel ?? 0,
                    CurrencyType.Exp => playerDb?.Exp ?? 0,
                    _ => 0
                };

                onSuccess?.Invoke(newAmount);
            }
            catch (Exception e)
            {
                transaction.Rollback();
                Console.WriteLine($"[DB Error] GiveQuestReward Failed. Player:{playerDbId}, Quest:{mainQuestId}-{subQuestId}, {e.Message}");
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