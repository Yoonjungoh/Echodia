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
