using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.DB;
using Server.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Server.DB
{
    // DB 작업 해주는 클래스
    public class DbTransaction : JobSerializer
    {
        public static DbTransaction Instance { get; } = new DbTransaction();

        #region Expression-tree 캐싱 (CurrencyType랑 PlayerDb 속성 자동 매핑)
        // 명명 규칙: CurrencyType.Gold == PlayerDb.Gold (이름이 일치해야 함)
        // 새 재화 추가 시 PlayerDb에 같은 이름의 속성만 추가하면 자동 동작
        private readonly struct CurrencyExprInfo
        {
            public readonly Func<PlayerDb, int> Getter;      // p => p.Gold (컴파일된 getter)
            public readonly Action<PlayerDb, int> Setter;    // (p, v) => p.Gold = v (컴파일된 setter)
            public CurrencyExprInfo(Func<PlayerDb, int> getter, Action<PlayerDb, int> setter)
            {
                Getter = getter;
                Setter = setter;
            }
        }

        private static readonly Dictionary<CurrencyType, CurrencyExprInfo> _currencyExprCache = new();
        private static readonly object _lock = new();

        // 재화 이름 자동화
        private static CurrencyExprInfo GetCurrencyExprInfo(CurrencyType currencyType)
        {
            lock (_lock)
            {
                if (_currencyExprCache.TryGetValue(currencyType, out var cached))
                    return cached;

                PropertyInfo propInfo = typeof(PlayerDb).GetProperty(currencyType.ToString())
                    ?? throw new InvalidOperationException(
                        $"PlayerDb에 '{currencyType}' 속성이 없습니다. " +
                        $"명명 규칙: CurrencyType 이름 == PlayerDb 속성 이름");

                var entityParam = Expression.Parameter(typeof(PlayerDb), "p");
                var propAccess = Expression.Property(entityParam, propInfo);

                var getter = Expression.Lambda<Func<PlayerDb, int>>(propAccess, entityParam).Compile();

                var valueParam = Expression.Parameter(typeof(int), "v");
                var setter = Expression.Lambda<Action<PlayerDb, int>>(
                    Expression.Assign(propAccess, valueParam), entityParam, valueParam).Compile();

                var entry = new CurrencyExprInfo(getter, setter);
                _currencyExprCache[currencyType] = entry;
                return entry;
            }
        }
        #endregion

        #region Quest

        public static void UpdateQuestStatus(int playerDbId, int mainQuestId, int subQuestId, QuestStatus status, Action callback = null)
        {
            Instance.Push(UpdateQuestStatus_Db, playerDbId, mainQuestId, subQuestId, status, callback);
        }

        private static void UpdateQuestStatus_Db(int playerDbId, int mainQuestId, int subQuestId, QuestStatus status, Action callback = null)
        {
            using (GameDbContext db = new GameDbContext())
            {
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
                int successRows = db.Quests
                    .Where(q => q.QuestDbId == quest.QuestDbId)
                    .ExecuteUpdate(s => s
                        .SetProperty(q => q.RequiredCount, q => quest.RequiredCount)
                        .SetProperty(q => q.Status, q => quest.Status)
                        .SetProperty(q => q.ClearedDate, q => quest.ClearedDate));

                if (successRows > 0)
                    callback?.Invoke();
                else
                    Console.WriteLine($"[DB Error] SaveQuestComplete Failed. QuestDbId: {quest.QuestDbId}");
            }
        }

        // 퀘스트 보상 수령: Completed → RewardClaimed, 재화+아이템 일괄 저장, 재화 패킷 전송
        // 아이템 슬롯 계산 및 인메모리 동기화는 호출 전(게임룸 스레드)에 완료되어야 함
        public static void GiveQuestReward(Player player, int mainQuestId, int subQuestId,
            List<(CurrencyType currencyType, int amount)> currencyRewards,
            Action onSuccess = null)
        {
            Instance.Push(() => GiveQuestReward_Db(player, mainQuestId, subQuestId, currencyRewards, onSuccess));
        }

        private static void GiveQuestReward_Db(Player player, int mainQuestId, int subQuestId,
            List<(CurrencyType currencyType, int amount)> currencyRewards,
            Action onSuccess)
        {
            using (GameDbContext db = new GameDbContext())
            {
                using var transaction = db.Database.BeginTransaction();
                try
                {
                    // 1. Completed 상태인 퀘스트를 RewardClaimed로 변경
                    int questRows = db.Quests
                        .Where(q => q.PlayerDbId == player.PlayerId
                                 && q.MainQuestId == mainQuestId
                                 && q.SubQuestId == subQuestId
                                 && q.Status == QuestStatus.Completed)
                        .ExecuteUpdate(s => s.SetProperty(q => q.Status, QuestStatus.RewardClaimed));

                    if (questRows == 0)
                    {
                        Console.WriteLine($"[DB Error] GiveQuestReward: 퀘스트가 Completed 상태가 아닙니다. Player:{player.PlayerId}, Quest:{mainQuestId}-{subQuestId}");
                        transaction.Rollback();
                        return;
                    }

                    // 2. 재화 보상
                    PlayerDb playerDb = db.Players.Find(player.PlayerId);
                    if (playerDb == null)
                    {
                        Console.WriteLine($"[DB Error] GiveQuestReward: Player not found. PlayerId:{player.PlayerId}");
                        transaction.Rollback();
                        return;
                    }

                    var newAmounts = new List<(CurrencyType currencyType, int newAmount)>();
                    foreach (var (currencyType, amount) in currencyRewards)
                    {
                        CurrencyExprInfo exprInfo = GetCurrencyExprInfo(currencyType);
                        int newAmount = exprInfo.Getter(playerDb) + amount;
                        exprInfo.Setter(playerDb, newAmount);
                        newAmounts.Add((currencyType, newAmount));
                    }

                    // 아이템 보상은 InventoryTracker가 로그아웃 시 일괄 저장

                    db.SaveChangesEx();
                    transaction.Commit();

                    // 3. 재화 업데이트 패킷 전송
                    foreach (var (currencyType, newAmount) in newAmounts)
                    {
                        player.Session?.Send(new S_UpdateCurrencyData { CurrencyType = currencyType, Amount = newAmount });
                    }

                    onSuccess?.Invoke();
                }
                catch (Exception e)
                {
                    transaction.Rollback();
                    Console.WriteLine($"[DB Error] GiveQuestReward Failed. Player:{player.PlayerId}, Quest:{mainQuestId}-{subQuestId}, {e.Message}");
                }
            }
        }

        // 퀘스트 보상 수령 후 다음 퀘스트를 DB에 생성하고 클라이언트에 알림
        // GiveQuestReward onSuccess 콜백에서 호출됨
        public static void CreateQuestChain(Player player, int mainQuestId, int subQuestId)
        {
            Instance.Push(() => CreateQuestChain_Db(player, mainQuestId, subQuestId));
        }

        private static void CreateQuestChain_Db(Player player, int mainQuestId, int subQuestId)
        {
            QuestObjectiveDefinitionMetaData objective = SpecDataManager.Instance.GetQuestObjectiveDefinition(mainQuestId, subQuestId);

            using (GameDbContext db = new GameDbContext())
            {
                QuestDb quest = new QuestDb()
                {
                    PlayerDbId = player.PlayerId,
                    MainQuestId = mainQuestId,
                    SubQuestId = subQuestId,
                    TargetId = objective?.TargetId ?? 0,
                    RequiredCount = 0,
                    Status = QuestStatus.NotAccepted,
                    StartedDate = DateTime.UtcNow,
                };

                db.Quests.Add(quest);
                bool success = db.SaveChangesEx();
                if (!success)
                {
                    Console.WriteLine($"[DB Error] CreateQuestChain: Save failed. Player:{player.PlayerId}, Quest:{mainQuestId}-{subQuestId}");
                    return;
                }

                player.Session?.Send(new S_CreateQuest
                {
                    MainQuestId = mainQuestId,
                    SubQuestId = subQuestId,
                });

                player.GameRoom?.Push(() => player.QuestTracker.Load());
            }
        }

        #endregion

        #region Item

        // 로그아웃 시 InventoryTracker가 호출 — 인메모리 변경 사항 일괄 DB 반영
        public static void SaveInventoryAsync(Player player, List<PlayerItemDb> toSave, List<int> toDeleteDbIds)
        {
            Instance.Push(() => SaveInventory_Db(player, toSave, toDeleteDbIds));
        }

        private static void SaveInventory_Db(Player player, List<PlayerItemDb> toSave, List<int> toDeleteDbIds)
        {
            using GameDbContext db = new GameDbContext();
            using var transaction = db.Database.BeginTransaction();
            try
            {
                // 삭제 (수량 0으로 소비된 아이템)
                if (toDeleteDbIds.Count > 0)
                {
                    db.PlayerItems
                        .Where(i => toDeleteDbIds.Contains(i.PlayerItemDbId))
                        .ExecuteDelete();
                }

                // 업데이트 / 신규 삽입
                foreach (PlayerItemDb item in toSave)
                {
                    if (item.PlayerItemDbId > 0)
                    {
                        db.PlayerItems
                            .Where(i => i.PlayerItemDbId == item.PlayerItemDbId)
                            .ExecuteUpdate(s => s.SetProperty(i => i.Count, item.Count));
                    }
                    else
                    {
                        db.PlayerItems.Add(new PlayerItemDb
                        {
                            PlayerDbId = item.PlayerDbId,
                            ItemId = item.ItemId,
                            Count = item.Count,
                            SlotIndex = item.SlotIndex,
                        });
                    }
                }

                db.SaveChangesEx();
                transaction.Commit();
            }
            catch (Exception e)
            {
                transaction.Rollback();
                Console.WriteLine($"[DB Error] SaveInventory Failed. Player:{player.PlayerId}, {e.Message}");
            }
        }

        #endregion

        #region Player

        public static void SavePlayerLogoutPosition(int playerDbId, float x, float y, float z, Action callback = null)
        {
            Instance.Push(SavePlayerLogoutPosition_Db, playerDbId, x, y, z, callback);
        }

        private static void SavePlayerLogoutPosition_Db(int playerDbId, float x, float y, float z, Action callback = null)
        {
            using (GameDbContext db = new GameDbContext())
            {
                int successRows = db.Players.Where(p => p.PlayerDbId == playerDbId).ExecuteUpdate(s => s
                    .SetProperty(p => p.LastPosX, p => x)
                    .SetProperty(p => p.LastPosY, p => y)
                    .SetProperty(p => p.LastPosZ, p => z));

                if (successRows > 0)
                    callback?.Invoke();
            }
        }

        // 재화를 절대값으로 저장하고, 저장 완료 후 S_UpdateCurrencyData 패킷 자동 전송
        // 새 CurrencyType 추가 시 PlayerDb에 같은 이름의 속성만 추가하면 자동 동작
        public static void SavePlayerCurrency(Player player, CurrencyType currencyType, int amount, Action callBack = null)
        {
            Instance.Push(SavePlayerCurrency_Db, player, currencyType, amount, callBack);
        }

        private static void SavePlayerCurrency_Db(Player player, CurrencyType currencyType, int amount, Action callBack)
        {
            using GameDbContext db = new GameDbContext();

            PlayerDb playerDb = db.Players.Find(player.PlayerId);
            if (playerDb == null)
            {
                Console.WriteLine($"[DB Error] SavePlayerCurrency: Player not found. PlayerId:{player.PlayerId}");
                return;
            }

            // expression cache로 switch 없이 자동 매핑
            GetCurrencyExprInfo(currencyType).Setter(playerDb, amount);
            db.SaveChanges();

            // 저장 완료 후 자동 패킷 전송
            player.Session?.Send(new S_UpdateCurrencyData { CurrencyType = currencyType, Amount = amount });
            callBack?.Invoke();
        }

        #endregion
    }
}
