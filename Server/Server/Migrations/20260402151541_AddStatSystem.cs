using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class AddStatSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StatCommonAttackDamage",
                table: "Player",
                newName: "StatSTR");

            migrationBuilder.AddColumn<int>(
                name: "JobType",
                table: "Player",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<float>(
                name: "StatCriticalDamage",
                table: "Player",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "StatCriticalRate",
                table: "Player",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<int>(
                name: "StatDEX",
                table: "Player",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StatINT",
                table: "Player",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StatLUK",
                table: "Player",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StatMagicDamage",
                table: "Player",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StatPhysicalDamage",
                table: "Player",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JobType",
                table: "Player");

            migrationBuilder.DropColumn(
                name: "StatCriticalDamage",
                table: "Player");

            migrationBuilder.DropColumn(
                name: "StatCriticalRate",
                table: "Player");

            migrationBuilder.DropColumn(
                name: "StatDEX",
                table: "Player");

            migrationBuilder.DropColumn(
                name: "StatINT",
                table: "Player");

            migrationBuilder.DropColumn(
                name: "StatLUK",
                table: "Player");

            migrationBuilder.DropColumn(
                name: "StatMagicDamage",
                table: "Player");

            migrationBuilder.DropColumn(
                name: "StatPhysicalDamage",
                table: "Player");

            migrationBuilder.RenameColumn(
                name: "StatSTR",
                table: "Player",
                newName: "StatCommonAttackDamage");
        }
    }
}
