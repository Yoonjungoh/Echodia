using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordHashV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "PasswordHashV1",
                table: "Account",
                type: "varbinary(48)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [Account] WHERE [PasswordHashV1] IS NOT NULL) THROW 51000, 'Cannot remove active password verifiers.', 1;");
            migrationBuilder.DropColumn(
                name: "PasswordHashV1",
                table: "Account");
        }
    }
}
