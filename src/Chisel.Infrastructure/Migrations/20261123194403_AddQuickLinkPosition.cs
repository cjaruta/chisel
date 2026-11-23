using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chisel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuickLinkPosition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Position",
                table: "QuickLinks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // The old table had no order: keep the order the links were added in (the table's own row order).
            migrationBuilder.Sql("UPDATE QuickLinks SET Position = (SELECT COUNT(*) FROM QuickLinks AS earlier WHERE earlier.rowid < QuickLinks.rowid)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Position",
                table: "QuickLinks");
        }
    }
}
