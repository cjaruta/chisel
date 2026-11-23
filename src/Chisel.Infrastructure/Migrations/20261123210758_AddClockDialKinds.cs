using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chisel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClockDialKinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DialFill",
                table: "ClockStyles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "ClockStyles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "MinuteTrack",
                table: "ClockStyles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Stopwatch",
                table: "ClockStyles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Tachymeter",
                table: "ClockStyles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DialFill",
                table: "ClockStyles");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "ClockStyles");

            migrationBuilder.DropColumn(
                name: "MinuteTrack",
                table: "ClockStyles");

            migrationBuilder.DropColumn(
                name: "Stopwatch",
                table: "ClockStyles");

            migrationBuilder.DropColumn(
                name: "Tachymeter",
                table: "ClockStyles");
        }
    }
}
