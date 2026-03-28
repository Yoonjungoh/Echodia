using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "StatAttackHalfAngleDeg",
                table: "Player",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "StatAttackHeight",
                table: "Player",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "StatAttackRange",
                table: "Player",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "StatCommonAttackCoolTime",
                table: "Player",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "StatCommonAttackDamage",
                table: "Player",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "StatDefense",
                table: "Player",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "StatMaxHp",
                table: "Player",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "StatMoveSpeed",
                table: "Player",
                type: "real",
                nullable: false,
                defaultValue: 0f);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StatAttackHalfAngleDeg",
                table: "Player");

            migrationBuilder.DropColumn(
                name: "StatAttackHeight",
                table: "Player");

            migrationBuilder.DropColumn(
                name: "StatAttackRange",
                table: "Player");

            migrationBuilder.DropColumn(
                name: "StatCommonAttackCoolTime",
                table: "Player");

            migrationBuilder.DropColumn(
                name: "StatCommonAttackDamage",
                table: "Player");

            migrationBuilder.DropColumn(
                name: "StatDefense",
                table: "Player");

            migrationBuilder.DropColumn(
                name: "StatMaxHp",
                table: "Player");

            migrationBuilder.DropColumn(
                name: "StatMoveSpeed",
                table: "Player");
        }
    }
}
