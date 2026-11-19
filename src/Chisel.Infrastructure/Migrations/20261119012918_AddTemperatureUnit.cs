using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chisel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTemperatureUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TemperatureUnit",
                table: "UserSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TemperatureUnit",
                table: "UserSettings");
        }
    }
}
