using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class PlayerSelectInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Player_Account_AccountDbId",
                table: "Player");

            migrationBuilder.AlterColumn<int>(
                name: "AccountDbId",
                table: "Player",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Gold",
                table: "Player",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PlayerId",
                table: "Player",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_Player_Account_AccountDbId",
                table: "Player",
                column: "AccountDbId",
                principalTable: "Account",
                principalColumn: "AccountDbId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Player_Account_AccountDbId",
                table: "Player");

            migrationBuilder.DropColumn(
                name: "Gold",
                table: "Player");

            migrationBuilder.DropColumn(
                name: "PlayerId",
                table: "Player");

            migrationBuilder.AlterColumn<int>(
                name: "AccountDbId",
                table: "Player",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_Player_Account_AccountDbId",
                table: "Player",
                column: "AccountDbId",
                principalTable: "Account",
                principalColumn: "AccountDbId");
        }
    }
}
