using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.DB
{
    public class GameDbContext : DbContext
    {
        public DbSet<AccountDb> Accounts { get; set; }

        public DbSet<PlayerDb> Players { get; set; }

        public DbSet<QuestDb> Quests { get; set; }

        public DbSet<PlayerItemDb> PlayerItems { get; set; }


        private static readonly ILoggerFactory _logger = LoggerFactory.Create(builder => { builder.AddConsole(); });
        private string _connectionString =
            Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
            ?? @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=GameDB;Integrated Security=True;Connect Timeout=30;Encrypt=False;Trust Server Certificate=False;Application Intent=ReadWrite;Multi Subnet Failover=False";
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options
                //.UseLoggerFactory(_logger)
                .UseSqlServer(_connectionString);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<AccountDb>()
                .HasIndex(a => a.AccountId)
                .IsUnique();

            builder.Entity<PlayerDb>()
                .HasIndex(p => p.Name)
                .IsUnique();

            builder.Entity<QuestDb>()
                .HasOne<PlayerDb>()
                .WithMany(p => p.Quests)
                .HasForeignKey(q => q.PlayerDbId);

            builder.Entity<PlayerItemDb>()
                .HasOne<PlayerDb>()
                .WithMany(p => p.Items)
                .HasForeignKey(i => i.PlayerDbId);
        }
    }
}
