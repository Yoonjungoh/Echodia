using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class init2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Account_AccountName",
                table: "Account");

            migrationBuilder.RenameColumn(
                name: "AccountName",
                table: "Account",
                newName: "AccountId");

            migrationBuilder.AddColumn<string>(
                name: "AccountPassword",
                table: "Account",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Account_AccountId",
                table: "Account",
                column: "AccountId",
                unique: true,
                filter: "[AccountId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Account_AccountId",
                table: "Account");

            migrationBuilder.DropColumn(
                name: "AccountPassword",
                table: "Account");

            migrationBuilder.RenameColumn(
                name: "AccountId",
                table: "Account",
                newName: "AccountName");

            migrationBuilder.CreateIndex(
                name: "IX_Account_AccountName",
                table: "Account",
                column: "AccountName",
                unique: true,
                filter: "[AccountName] IS NOT NULL");
        }
    }
}
