using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.DB;
using Server.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
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

        // CurrencyTracker.Load()에서 PlayerDb 속성 값을 읽을 때 사용
        // expression tree 캐시를 공유해 컴파일 비용 없음
        public static int GetCurrencyValue(PlayerDb playerDb, CurrencyType currencyType)
        {
            return GetCurrencyExprInfo(currencyType).Getter(playerDb);
        }
        #endregion

        #region Quest

        public static void AcceptQuest(int playerDbId, int mainQuestId, int subQuestId, Action callback)
        {
            Instance.Push(() =>
            {
                using var db = new GameDbContext();
                if (TryAcceptQuest(db, playerDbId, mainQuestId, subQuestId))
                    callback?.Invoke();
            });
        }

        internal static bool TryAcceptQuest(GameDbContext db, int playerDbId, int mainQuestId, int subQuestId)
        {
            return db.Quests.Where(q => q.PlayerDbId == playerDbId && q.MainQuestId == mainQuestId
                                     && q.SubQuestId == subQuestId && q.Status == QuestStatus.NotAccepted)
                .ExecuteUpdate(s => s.SetProperty(q => q.Status, QuestStatus.Proceeding)) > 0;
        }

        public static void SaveQuestComplete(QuestDb quest, Action callback = null)
        {
            Instance.Push(() =>
            {
                using var db = new GameDbContext();
                if (TryCompleteQuest(db, quest))
                    callback?.Invoke();
            });
        }

        internal static bool TryCompleteQuest(GameDbContext db, QuestDb quest)
        {
            return db.Quests.Where(q => q.QuestDbId == quest.QuestDbId && q.PlayerDbId == quest.PlayerDbId
                                     && q.Status == QuestStatus.Proceeding)
                .ExecuteUpdate(s => s
                    .SetProperty(q => q.RequiredCount, quest.RequiredCount)
                    .SetProperty(q => q.Status, QuestStatus.Completed)
                    .SetProperty(q => q.ClearedDate, quest.ClearedDate)) == 1;
        }

        internal static void SetCurrencyValue(PlayerDb player, CurrencyType type, int amount)
        {
            GetCurrencyExprInfo(type).Setter(player, amount);
        }
        #endregion

        #region Stats

        public static void SavePlayerStatsAsync(Player player)
        {
            Instance.Push(() => SavePlayerStats_Db(player));
        }

        private static void SavePlayerStats_Db(Player player)
        {
            using GameDbContext db = new GameDbContext();
            PlayerDb playerDb = db.Players
                .Where(p => p.PlayerDbId == player.PlayerId)
                .FirstOrDefault();

            if (playerDb == null)
                return;

            Stat s = player.ObjectState.Stat;
            playerDb.StatMaxHp              = s.MaxHp;
            playerDb.StatCommonAttackCoolTime = s.CommonAttackCoolTime;
            playerDb.StatAttackRange        = s.AttackRange;
            playerDb.StatDefense            = s.Defense;
            playerDb.StatMoveSpeed          = s.MoveSpeed;
            playerDb.StatAttackHalfAngleDeg = s.AttackHalfAngleDeg;
            playerDb.StatAttackHeight       = s.AttackHeight;
            playerDb.StatPhysicalDamage     = s.PhysicalDamage;
            playerDb.StatMagicDamage        = s.MagicDamage;
            playerDb.StatSTR                = s.Str;
            playerDb.StatDEX                = s.Dex;
            playerDb.StatINT                = s.StatInt;
            playerDb.StatLUK                = s.Luk;
            playerDb.StatCriticalRate       = s.CriticalRate;
            playerDb.StatCriticalDamage     = s.CriticalDamage;
            playerDb.JobType                = s.JobType;
            playerDb.StatMaxMp              = s.MaxMp;
            playerDb.StatMpRegen            = player.MpRegen;

            db.SaveChangesEx();
        }

        #endregion

        #region Player

        public static void SavePlayerLogoutState(int playerDbId, Vector3 pos, int mapId, Action callback = null)
        {
            Instance.Push(SavePlayerLogoutState_Db, playerDbId, pos, mapId, callback);
        }

        private static void SavePlayerLogoutState_Db(int playerDbId, Vector3 pos, int mapId, Action callback = null)
        {
            using (GameDbContext db = new GameDbContext())
            {
                int successRows = db.Players.Where(p => p.PlayerDbId == playerDbId).ExecuteUpdate(s => s
                    .SetProperty(p => p.LastPosX,  p => pos.X)
                    .SetProperty(p => p.LastPosY,  p => pos.Y)
                    .SetProperty(p => p.LastPosZ,  p => pos.Z)
                    .SetProperty(p => p.LastMapId, p => mapId));

                if (successRows > 0)
                    callback?.Invoke();
            }
        }

        #endregion
    }
}
