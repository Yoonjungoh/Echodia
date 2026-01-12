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

        private static readonly ILoggerFactory _logger= LoggerFactory.Create(builder => { builder.AddConsole(); } );
        // TODO - JSON으로 옮기기
        private string _connectionString = @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=GameDB;Integrated Security=True;Connect Timeout=30;Encrypt=False;Trust Server Certificate=False;Application Intent=ReadWrite;Multi Subnet Failover=False";
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options
                .UseLoggerFactory(_logger)
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
        }
    }
}
