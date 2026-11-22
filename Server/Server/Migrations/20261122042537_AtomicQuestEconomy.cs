using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class AtomicQuestEconomy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [Quest] GROUP BY [PlayerDbId], [MainQuestId], [SubQuestId] HAVING COUNT_BIG(*) > 1) THROW 51001, 'Duplicate player quest rows require review before enabling atomic claims.', 1;");

            migrationBuilder.DropIndex(
                name: "IX_Quest_PlayerDbId",
                table: "Quest");

            migrationBuilder.AddColumn<long>(
                name: "EconomyRevision",
                table: "Player",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_Quest_PlayerDbId_MainQuestId_SubQuestId",
                table: "Quest",
                columns: new[] { "PlayerDbId", "MainQuestId", "SubQuestId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Quest_PlayerDbId_MainQuestId_SubQuestId",
                table: "Quest");

            migrationBuilder.DropColumn(
                name: "EconomyRevision",
                table: "Player");

            migrationBuilder.CreateIndex(
                name: "IX_Quest_PlayerDbId",
                table: "Quest",
                column: "PlayerDbId");
        }
    }
}
