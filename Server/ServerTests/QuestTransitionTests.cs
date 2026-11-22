using Google.Protobuf.Protocol;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Server.DB;
using Server.Game;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Server.Migrations;

namespace ServerTests;

public class QuestTransitionTests
{
    [LocalDbFact]
    public void AcceptAndCompleteAdvanceOnlyFromTheirExpectedState()
    {
        using var db = new QuestTestDatabase();
        db.Database.EnsureCreated();
        try
        {
            var player = new PlayerDb { Name = "quest-test", Account = new AccountDb { AccountId = "quest-test" } };
            var quest = new QuestDb { MainQuestId = 1, SubQuestId = 1, Status = QuestStatus.NotAccepted };
            player.Quests.Add(quest);
            db.Add(player);
            db.SaveChanges();

            Assert.False(DbTransaction.TryCompleteQuest(db, quest));
            Assert.False(DbTransaction.TryAcceptQuest(db, player.PlayerDbId + 1, 1, 1));
            using var second = db.OpenAnotherContext();
            var accepted = new bool[2];
            Parallel.Invoke(
                () => accepted[0] = DbTransaction.TryAcceptQuest(db, player.PlayerDbId, 1, 1),
                () => accepted[1] = DbTransaction.TryAcceptQuest(second, player.PlayerDbId, 1, 1));
            Assert.Single(accepted, succeeded => succeeded);
            Assert.False(DbTransaction.TryAcceptQuest(db, player.PlayerDbId, 1, 1));

            quest.RequiredCount = 5;
            quest.ClearedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Assert.True(DbTransaction.TryCompleteQuest(db, quest));
            Assert.False(DbTransaction.TryAcceptQuest(db, player.PlayerDbId, 1, 1));
            Assert.False(DbTransaction.TryCompleteQuest(db, quest));

            db.Quests.ExecuteUpdate(s => s.SetProperty(q => q.Status, QuestStatus.RewardClaimed));
            Assert.False(DbTransaction.TryAcceptQuest(db, player.PlayerDbId, 1, 1));
            Assert.False(DbTransaction.TryCompleteQuest(db, quest));
            var persisted = db.Quests.AsNoTracking().Single();
            Assert.Equal(QuestStatus.RewardClaimed, persisted.Status);
            Assert.Equal(5, persisted.RequiredCount);
            Assert.Equal(quest.ClearedDate, persisted.ClearedDate);
        }
        finally
        {
            db.Database.EnsureDeleted();
        }
    }

    [LocalDbFact]
    public void ConcurrentClaimReconnectAndDelayedAutosaveKeepOneReward()
    {
        using var initial = new QuestTestDatabase();
        initial.Database.EnsureCreated();
        try
        {
            var player = new PlayerDb { Name = "owner", Gold = 100, Account = new AccountDb { AccountId = "owner" } };
            player.Quests.Add(new QuestDb { MainQuestId = 11, SubQuestId = 2, Status = QuestStatus.Completed });
            var firstStack = new PlayerItemDb { ItemId = 200010, Count = 3, SlotIndex = 0 };
            player.Items.Add(firstStack);
            var otherBag = new PlayerItemDb { ItemId = 100010, Count = 1, SlotIndex = 0 };
            player.Items.Add(otherBag);
            initial.Add(player);
            initial.SaveChanges();
            using var a = initial.OpenAnotherContext();
            using var b = initial.OpenAnotherContext();
            int playerId = player.PlayerDbId;
            EconomyWrite Attempt(GameDbContext db)
            {
                var batch = new EconomyBatch(playerId, player.AccountDbId, 0);
                var grant = new QuestGrant(11, 2);
                grant.Next.Add(new NextQuest(11, 3, 5, 0));
                var bag = new Dictionary<(ItemType, int), PlayerItemDb> { [(ItemType.Consumable, 0)] = firstStack };
                var meta = new Server.Data.ItemMetaData { Id = firstStack.ItemId, ItemType = ItemType.Consumable, MaxStack = 4 };
                Assert.True(Server.QuestManager.AllocateItems(bag, new HashSet<(ItemType, int)>(), batch, grant, meta, 3, 2));
                batch.Currency[CurrencyType.Gold] = 110;
                return batch.Save(db, grant);
            }
            var outcomes = new EconomyWrite[2];
            Parallel.Invoke(() => outcomes[0] = Attempt(a), () => outcomes[1] = Attempt(b));
            Assert.Single(outcomes, result => result == EconomyWrite.Saved);

            using var reconnect = initial.OpenAnotherContext();
            var playerRow = reconnect.Players.AsNoTracking().Single();
            Assert.Equal(1, playerRow.EconomyRevision);
            Assert.Equal(110, playerRow.Gold);
            var retry = new EconomyBatch(playerId, player.AccountDbId, playerRow.EconomyRevision);
            retry.Currency[CurrencyType.Gold] = 120;
            Assert.Equal(EconomyWrite.IneligibleQuest, retry.Save(reconnect, new QuestGrant(11, 2)));
            var oldAutosave = new EconomyBatch(playerId, player.AccountDbId, 0);
            oldAutosave.Currency[CurrencyType.Gold] = 100;
            Assert.Equal(EconomyWrite.StaleOwner, oldAutosave.Save(reconnect));
            var wrongOwner = new EconomyBatch(playerId, player.AccountDbId + 1, 1);
            Assert.Equal(EconomyWrite.StaleOwner, wrongOwner.Save(reconnect));
            Assert.Equal(110, reconnect.Players.AsNoTracking().Single().Gold);
            Assert.Equal(1, reconnect.Players.AsNoTracking().Single().EconomyRevision);
            Assert.Equal(4, reconnect.PlayerItems.AsNoTracking().Single(i => i.PlayerItemDbId == firstStack.PlayerItemDbId).Count);
            Assert.Equal(1, reconnect.PlayerItems.AsNoTracking().Single(i => i.PlayerItemDbId == otherBag.PlayerItemDbId).Count);
            Assert.Equal(2, reconnect.PlayerItems.AsNoTracking().Single(i => i.SlotIndex == 1).Count);
            Assert.Equal(2, reconnect.Quests.Count());
            Assert.Single(reconnect.Quests.Where(q => q.Status == QuestStatus.RewardClaimed));
        }
        finally { initial.Database.EnsureDeleted(); }
    }

    [LocalDbFact]
    public void FailedGrantRollsBackWalletItemsChainAndQuestTogether()
    {
        using var seed = new QuestTestDatabase();
        seed.Database.EnsureCreated();
        try
        {
            var player = new PlayerDb { Name = "rollback", Gold = 100, Account = new AccountDb { AccountId = "rollback" } };
            player.Quests.Add(new QuestDb { MainQuestId = 1, SubQuestId = 1, Status = QuestStatus.Completed });
            seed.Add(player);
            seed.SaveChanges();
            seed.Database.ExecuteSqlRaw("CREATE TRIGGER [FailTestInsert] ON [PlayerItem] AFTER INSERT AS THROW 51001, 'Injected test rollback', 1;");
            using (var write = seed.OpenAnotherContext())
            {
                var batch = new EconomyBatch(player.PlayerDbId, player.AccountDbId, 0);
                batch.Currency[CurrencyType.Gold] = 200;
                var item = new PlayerItemDb { PlayerDbId = player.PlayerDbId, ItemId = 42, Count = 1, SlotIndex = 0 };
                batch.Items[item] = item.Snapshot();
                var grant = new QuestGrant(1, 1);
                grant.Next.Add(new NextQuest(1, 2, 10, 0));
                Assert.Throws<DbUpdateException>(() => batch.Save(write, grant));
            }
            using var read = seed.OpenAnotherContext();
            var saved = read.Players.Single();
            Assert.Equal(100, saved.Gold);
            Assert.Equal(0, saved.EconomyRevision);
            Assert.Empty(read.PlayerItems);
            Assert.Equal(QuestStatus.Completed, read.Quests.Single().Status);
        }
        finally { seed.Database.EnsureDeleted(); }
    }

    [Fact]
    public void FullOrAmbiguousBagLeavesOriginalItemsUntouched()
    {
        var batch = new EconomyBatch(4, 2, 0);
        var grant = new QuestGrant(1, 1);
        var meta = new Server.Data.ItemMetaData { Id = 20, ItemType = ItemType.Consumable, MaxStack = 3 };
        var existing = new PlayerItemDb { PlayerDbId = 4, ItemId = 20, Count = 2 };
        var key = (ItemType.Consumable, 0);
        var bag = new Dictionary<(ItemType, int), PlayerItemDb> { [key] = existing };
        Assert.False(Server.QuestManager.AllocateItems(bag, new HashSet<(ItemType, int)>(), batch, grant, meta, 3, 1));
        Assert.Equal(2, existing.Count);
        batch = new EconomyBatch(4, 2, 0);
        grant = new QuestGrant(1, 1);
        Assert.False(Server.QuestManager.AllocateItems(bag, new HashSet<(ItemType, int)> { key }, batch, grant, meta, 1, 1));
        Assert.Empty(batch.Items);
        Assert.Equal(2, existing.Count);
    }

    [LocalDbFact]
    public void RoomContinuesWhileClaimIsPendingAndFinalSaveFollowsQueuedMutation()
    {
        using var seed = new QuestTestDatabase();
        seed.Database.EnsureCreated();
        try
        {
            var row = new PlayerDb { Name = "ordered", Gold = 100, Account = new AccountDb { AccountId = "ordered" } };
            row.Quests.Add(new QuestDb { MainQuestId = 1, SubQuestId = 1, Status = QuestStatus.Completed });
            seed.Add(row);
            seed.SaveChanges();
            var room = new GameRoom(1, 1, 1);
            var player = new Player { PlayerId = row.PlayerDbId, GameRoom = room };
            var economy = new PlayerEconomy(seed.OpenAnotherContext);
            Assert.Throws<InvalidOperationException>(() => economy.Load(player, row.AccountDbId + 1));
            economy.Load(player, row.AccountDbId);
            var batch = economy.Capture(player);
            var grant = new QuestGrant(1, 1);
            grant.Balances[CurrencyType.Gold] = batch.Currency[CurrencyType.Gold] = 110;
            var ticks = new List<string>();
            economy.Submit(player, batch, grant, () => ticks.Add("claim"));
            economy.RunInRoom(player, room, () =>
            {
                player.CurrencyTracker.Set(CurrencyType.Gold, player.CurrencyTracker.Get(CurrencyType.Gold) + 5);
                ticks.Add("kill");
            });
            void Leave()
            {
                if (economy.PrepareLeave(player, Leave)) return;
                player.GameRoom = null;
                ticks.Add("leave");
            }
            Leave();
            room.Push(() => ticks.Add("other player's movement"));
            room.Flush();
            Assert.Equal(new[] { "other player's movement" }, ticks);

            DbTransaction.Instance.Flush();
            room.Flush();
            Assert.Equal(115, player.CurrencyTracker.Get(CurrencyType.Gold));
            Assert.Equal(new[] { "other player's movement", "claim", "kill" }, ticks);
            economy.RunInRoom(player, room, () => ticks.Add("late command"));
            DbTransaction.Instance.Flush();
            room.Flush();
            Assert.Equal(new[] { "other player's movement", "claim", "kill", "leave" }, ticks);

            using var reconnect = seed.OpenAnotherContext();
            Assert.Equal(115, reconnect.Players.Single().Gold);
            Assert.Equal(2, reconnect.Players.Single().EconomyRevision);
            Assert.Equal(QuestStatus.RewardClaimed, reconnect.Quests.Single().Status);
        }
        finally { seed.Database.EnsureDeleted(); }
    }

    [LocalDbFact]
    public void FailedClaimPreservesPriorDirtyGoldAndLeavesQuestAvailableAfterReconnect()
    {
        using var seed = new QuestTestDatabase();
        seed.Database.EnsureCreated();
        try
        {
            var row = new PlayerDb { Name = "recovery", Gold = 100, Account = new AccountDb { AccountId = "recovery" } };
            row.Quests.Add(new QuestDb { MainQuestId = 3, SubQuestId = 1, Status = QuestStatus.Completed });
            seed.Add(row);
            seed.SaveChanges();
            seed.Database.ExecuteSqlRaw("CREATE TRIGGER [FailClaim] ON [Quest] AFTER UPDATE AS THROW 51005, 'Injected claim rollback', 1;");
            var room = new GameRoom(1, 1, 1);
            var player = new Player { PlayerId = row.PlayerDbId, GameRoom = room };
            var economy = new PlayerEconomy(seed.OpenAnotherContext);
            economy.Load(player, row.AccountDbId);
            player.CurrencyTracker.Set(CurrencyType.Gold, 105); // earned before the request
            var batch = economy.Capture(player);
            var grant = new QuestGrant(3, 1);
            grant.Balances[CurrencyType.Gold] = batch.Currency[CurrencyType.Gold] = 125;
            bool confirmed = false;
            economy.Submit(player, batch, grant, () => confirmed = true);
            DbTransaction.Instance.Flush();
            room.Flush();
            bool left = false;
            void Leave()
            {
                if (!economy.PrepareLeave(player, Leave)) left = true;
            }
            Leave();
            Assert.False(left);
            Assert.True(SpinWait.SpinUntil(() =>
            {
                room.Flush();
                DbTransaction.Instance.Flush();
                return left;
            }, TimeSpan.FromSeconds(8)));
            Assert.False(confirmed);
            using var reconnect = seed.OpenAnotherContext();
            Assert.Equal(105, reconnect.Players.Single().Gold);
            Assert.Equal(1, reconnect.Players.Single().EconomyRevision);
            Assert.Equal(QuestStatus.Completed, reconnect.Quests.Single().Status);
        }
        finally { seed.Database.EnsureDeleted(); }
    }

    [LocalDbFact]
    public void MigrationPreservesLegacyBagRowsAndRefusesDuplicateQuestsBeforeChanges()
    {
        using var db = new QuestTestDatabase();
        db.Database.EnsureCreated();
        try
        {
            var row = new PlayerDb { Name = "legacy", Account = new AccountDb { AccountId = "legacy" } };
            row.Quests.Add(new QuestDb { MainQuestId = 8, SubQuestId = 1, Status = QuestStatus.RewardClaimed });
            for (int i = 1; i <= 3; i++) row.Items.Add(new PlayerItemDb { ItemId = 100 + i, Count = i, SlotIndex = 0 });
            db.Add(row);
            db.SaveChanges();
            var migration = new AtomicQuestEconomy { ActiveProvider = "Microsoft.EntityFrameworkCore.SqlServer" };
            var sql = db.GetService<IMigrationsSqlGenerator>();
            void Apply(IReadOnlyList<Microsoft.EntityFrameworkCore.Migrations.Operations.MigrationOperation> ops)
            {
                using var tx = db.Database.BeginTransaction();
                foreach (var command in sql.Generate(ops))
                    db.Database.ExecuteSqlRaw(command.CommandText);
                tx.Commit();
            }
            Apply(migration.DownOperations);
            var duplicate = new QuestDb { PlayerDbId = row.PlayerDbId, MainQuestId = 8, SubQuestId = 1 };
            db.Add(duplicate);
            db.SaveChanges();
            Assert.Equal(51001, Assert.Throws<SqlException>(() => Apply(migration.UpOperations)).Number);
            Assert.Equal(2, db.Quests.Count());
            db.Quests.Where(q => q.QuestDbId == duplicate.QuestDbId).ExecuteDelete();
            Apply(migration.UpOperations);
            db.ChangeTracker.Clear();
            Assert.Equal(0, db.Players.Single().EconomyRevision);
            Assert.Equal(QuestStatus.RewardClaimed, db.Quests.Single().Status);
            Assert.Equal(new[] { 1, 2, 3 }, db.PlayerItems.OrderBy(i => i.ItemId).Select(i => i.Count));
        }
        finally { db.Database.EnsureDeleted(); }
    }

    // Each test owns a new database on the current Windows user's LocalDB instance.
    private sealed class QuestTestDatabase : GameDbContext
    {
        private readonly string _database;

        public QuestTestDatabase() : this("EchodiaQuestTest_" + Guid.NewGuid().ToString("N")) { }
        private QuestTestDatabase(string database) { _database = database; }
        public QuestTestDatabase OpenAnotherContext() => new(_database);

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            var connection = new SqlConnectionStringBuilder
            {
                DataSource = @"(localdb)\MSSQLLocalDB",
                InitialCatalog = _database,
                IntegratedSecurity = true,
                TrustServerCertificate = true
            };
            options.UseSqlServer(connection.ConnectionString);
        }
    }

    private sealed class LocalDbFactAttribute : FactAttribute
    {
        public LocalDbFactAttribute()
        {
            if (!OperatingSystem.IsWindows() || !HasLocalDb())
                Skip = "Requires Microsoft SQL Server Express LocalDB.";
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static bool HasLocalDb()
        {
            using var versions = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Microsoft SQL Server Local DB\Installed Versions");
            return versions?.SubKeyCount > 0;
        }
    }
}
