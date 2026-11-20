using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chisel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReworkClockSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlarmEnabled",
                table: "ClockTools");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "ClockTools");

            migrationBuilder.DropColumn(
                name: "StopwatchEnabled",
                table: "ClockTools");

            migrationBuilder.DropColumn(
                name: "Strip",
                table: "ClockTools");

            migrationBuilder.DropColumn(
                name: "TimerEnabled",
                table: "ClockTools");

            migrationBuilder.AddColumn<string>(
                name: "SlotsJson",
                table: "ClockTools",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SlotsJson",
                table: "ClockTools");

            migrationBuilder.AddColumn<bool>(
                name: "AlarmEnabled",
                table: "ClockTools",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Mode",
                table: "ClockTools",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "StopwatchEnabled",
                table: "ClockTools",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Strip",
                table: "ClockTools",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "TimerEnabled",
                table: "ClockTools",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
