using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chisel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendarSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstDay = table.Column<int>(type: "INTEGER", nullable: true),
                    ShowWeekNumbers = table.Column<bool>(type: "INTEGER", nullable: false),
                    MarkedDays = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarSettings");
        }
    }
}
