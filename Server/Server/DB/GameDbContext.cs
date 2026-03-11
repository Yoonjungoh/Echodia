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


        private static readonly ILoggerFactory _logger = LoggerFactory.Create(builder => { builder.AddConsole(); });
        // TODO - JSON으로 옮기기
        private string _connectionString = @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=GameDB;Integrated Security=True;Connect Timeout=30;Encrypt=False;Trust Server Certificate=False;Application Intent=ReadWrite;Multi Subnet Failover=False";
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
                .HasOne<PlayerDb>()      // 퀘스트는 하나의 플레이어에 속함
                .WithMany(p => p.Quests)    // 플레이어는 여러 퀘스트를 가질 수 있음
                .HasForeignKey(q => q.PlayerDbId); // FK 설정
        }
    }
}
