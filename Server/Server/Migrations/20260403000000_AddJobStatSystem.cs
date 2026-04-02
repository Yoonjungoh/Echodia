using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class AddJobStatSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 직업 타입
            migrationBuilder.AddColumn<int>(
                name: "JobType",
                table: "Player",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // 기본 공격력 (물리/마법) — StatCommonAttackDamage 대체
            migrationBuilder.AddColumn<int>(
                name: "StatPhysicalDamage",
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

            // 주요 스탯
            migrationBuilder.AddColumn<int>(
                name: "StatSTR",
                table: "Player",
                type: "int",
                nullable: false,
                defaultValue: 0);

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

            // 크리티컬
            migrationBuilder.AddColumn<float>(
                name: "StatCriticalRate",
                table: "Player",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "StatCriticalDamage",
                table: "Player",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            // 기존 CommonAttackDamage 제거
            migrationBuilder.DropColumn(
                name: "StatCommonAttackDamage",
                table: "Player");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "JobType",           table: "Player");
            migrationBuilder.DropColumn(name: "StatPhysicalDamage", table: "Player");
            migrationBuilder.DropColumn(name: "StatMagicDamage",    table: "Player");
            migrationBuilder.DropColumn(name: "StatSTR",            table: "Player");
            migrationBuilder.DropColumn(name: "StatDEX",            table: "Player");
            migrationBuilder.DropColumn(name: "StatINT",            table: "Player");
            migrationBuilder.DropColumn(name: "StatLUK",            table: "Player");
            migrationBuilder.DropColumn(name: "StatCriticalRate",   table: "Player");
            migrationBuilder.DropColumn(name: "StatCriticalDamage", table: "Player");

            migrationBuilder.AddColumn<int>(
                name: "StatCommonAttackDamage",
                table: "Player",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
