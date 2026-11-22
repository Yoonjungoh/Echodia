using Google.Protobuf.Protocol;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Server;
using Server.DB;

namespace ServerTests;

public class CharacterOwnershipTests
{
    private sealed class CharacterTestDb(SqliteConnection connection) : GameDbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseSqlite(connection);
    }

    [Fact]
    public void CharacterLookupsAndDeletionRequireTheSameOwner()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = new CharacterTestDb(connection);
        db.Database.EnsureCreated();
        var owner = new AccountDb { AccountId = "owner" };
        var other = new AccountDb { AccountId = "other" };
        db.Accounts.AddRange(owner, other);
        db.SaveChanges();
        var owned = new PlayerDb(owner.AccountDbId, "owned");
        var foreign = new PlayerDb(other.AccountDbId, "foreign");
        db.Players.AddRange(owned, foreign);
        db.SaveChanges();
        db.ChangeTracker.Clear();

        var selected = db.OwnedPlayer(owner.AccountDbId, owned.PlayerDbId);
        Assert.True(selected.Any());
        Assert.Equal("owned", selected.Select(p => p.Name).Single());
        foreach (int unavailable in new[] { foreign.PlayerDbId, 0, -1, int.MaxValue })
        {
            var unavailablePlayer = db.OwnedPlayer(owner.AccountDbId, unavailable);
            Assert.False(unavailablePlayer.Any());
            Assert.Null(unavailablePlayer.SingleOrDefault());
            Assert.Equal(0, unavailablePlayer.ExecuteDelete());
        }
        Assert.Equal(1, selected.ExecuteDelete());
        Assert.Equal(0, selected.ExecuteDelete());
        Assert.Equal(foreign.PlayerDbId, db.Players.Select(p => p.PlayerDbId).Single());
    }

    [Fact]
    public void SignedOutSessionStaysAtLoginThroughCharacterRequests()
    {
        var session = new ClientSession();
        session.HandleSelectServer(1, 1);
        session.HandleSelectPlayer(1, 1, 1);
        session.HandleEnterGame(1, 1, 1);
        session.HandleRequestInitGameRoomData();
        session.HandleDeletePlayer(1);
        Assert.Equal(ClientServerState.Login, session.ClientServerState);
        Assert.Equal(0, session.AccountId);
        Assert.Null(session.MyPlayer);
    }
}
