using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chisel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClockStyle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClockStyles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Alignment = table.Column<int>(type: "INTEGER", nullable: false),
                    Shape = table.Column<int>(type: "INTEGER", nullable: false),
                    BorderWidth = table.Column<double>(type: "REAL", nullable: false),
                    Markers = table.Column<int>(type: "INTEGER", nullable: false),
                    EmphasizeQuarters = table.Column<bool>(type: "INTEGER", nullable: false),
                    Numerals = table.Column<int>(type: "INTEGER", nullable: false),
                    Hands = table.Column<int>(type: "INTEGER", nullable: false),
                    HourThickness = table.Column<double>(type: "REAL", nullable: false),
                    MinuteThickness = table.Column<double>(type: "REAL", nullable: false),
                    SecondThickness = table.Column<double>(type: "REAL", nullable: false),
                    ShowSeconds = table.Column<bool>(type: "INTEGER", nullable: false),
                    DialColor = table.Column<string>(type: "TEXT", nullable: true),
                    MarkerColor = table.Column<string>(type: "TEXT", nullable: true),
                    NumeralColor = table.Column<string>(type: "TEXT", nullable: true),
                    HourColor = table.Column<string>(type: "TEXT", nullable: true),
                    MinuteColor = table.Column<string>(type: "TEXT", nullable: true),
                    SecondColor = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClockStyles", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClockStyles");
        }
    }
}
