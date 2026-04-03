using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class CriticalRateToInt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 기존 float 값(0~100)을 만분율 int(0~10000)로 변환한 뒤 컬럼 타입 변경
            migrationBuilder.Sql("UPDATE [Player] SET [StatCriticalRate] = CAST([StatCriticalRate] * 100 AS INT)");

            migrationBuilder.AlterColumn<int>(
                name: "StatCriticalRate",
                table: "Player",
                type: "int",
                nullable: false,
                oldClrType: typeof(float),
                oldType: "real");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<float>(
                name: "StatCriticalRate",
                table: "Player",
                type: "real",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}
