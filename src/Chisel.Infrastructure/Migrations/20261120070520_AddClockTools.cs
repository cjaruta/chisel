using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chisel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClockTools : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClockTools",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    AlarmEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    TimerEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    StopwatchEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Strip = table.Column<int>(type: "INTEGER", nullable: false),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    TimerDurationTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    TimerBankedTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    TimerStartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    StopwatchBankedTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    StopwatchStartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    StopwatchLapsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClockTools", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClockTools");
        }
    }
}
