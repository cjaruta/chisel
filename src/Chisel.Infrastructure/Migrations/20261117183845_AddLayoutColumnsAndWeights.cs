using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chisel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLayoutColumnsAndWeights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Column",
                table: "WidgetPlacements",
                newName: "ColumnIndex");

            migrationBuilder.AddColumn<double>(
                name: "HeightWeight",
                table: "WidgetPlacements",
                type: "REAL",
                nullable: false,
                defaultValue: 1.0);

            migrationBuilder.CreateTable(
                name: "LayoutColumns",
                columns: table => new
                {
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    WidthWeight = table.Column<double>(type: "REAL", nullable: false, defaultValue: 1.0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LayoutColumns", x => x.Position);
                });

            // The old Column was an enum: 0 = Side (the right-hand list), 1 = Main (the left).
            // Columns are now ordered left to right, so Main becomes column 0 and Side column 1.
            migrationBuilder.Sql("UPDATE WidgetPlacements SET ColumnIndex = CASE ColumnIndex WHEN 1 THEN 0 ELSE 1 END;");

            // Give every column in use a width; the left column was 1.3 times as wide as the right.
            migrationBuilder.Sql(
                "INSERT INTO LayoutColumns (Position, WidthWeight) " +
                "SELECT DISTINCT ColumnIndex, CASE ColumnIndex WHEN 0 THEN 1.3 ELSE 1.0 END FROM WidgetPlacements;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LayoutColumns");

            // Back to the old enum; anything past the second column lands in Side.
            migrationBuilder.Sql("UPDATE WidgetPlacements SET ColumnIndex = CASE ColumnIndex WHEN 0 THEN 1 ELSE 0 END;");

            migrationBuilder.DropColumn(
                name: "HeightWeight",
                table: "WidgetPlacements");

            migrationBuilder.RenameColumn(
                name: "ColumnIndex",
                table: "WidgetPlacements",
                newName: "Column");
        }
    }
}
