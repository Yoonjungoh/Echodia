using Google.Protobuf.Protocol;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Server.DB;

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
