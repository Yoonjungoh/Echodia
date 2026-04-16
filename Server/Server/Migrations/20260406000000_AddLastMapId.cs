using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class AddLastMapId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 컬럼이 이미 존재할 경우를 대비해 IF NOT EXISTS 조건으로 추가
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = 'Player' AND COLUMN_NAME = 'LastMapId'
                )
                BEGIN
                    ALTER TABLE [Player] ADD [LastMapId] int NOT NULL DEFAULT 1;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastMapId",
                table: "Player");
        }
    }
}
