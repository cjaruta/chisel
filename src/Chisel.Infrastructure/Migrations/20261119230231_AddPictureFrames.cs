using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chisel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPictureFrames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PictureKeepOriginals",
                table: "UserSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "PictureMultiDrop",
                table: "UserSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "FrameOptions",
                columns: table => new
                {
                    FrameId = table.Column<string>(type: "TEXT", nullable: false),
                    Fit = table.Column<int>(type: "INTEGER", nullable: false),
                    BlurFill = table.Column<bool>(type: "INTEGER", nullable: false),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    IntervalSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Transition = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentIndex = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FrameOptions", x => x.FrameId);
                });

            migrationBuilder.CreateTable(
                name: "FramePictures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FrameId = table.Column<string>(type: "TEXT", nullable: false),
                    AssetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    FocalX = table.Column<double>(type: "REAL", nullable: false),
                    FocalY = table.Column<double>(type: "REAL", nullable: false),
                    Zoom = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FramePictures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PictureAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Hash = table.Column<string>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    MimeType = table.Column<string>(type: "TEXT", nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    Bytes = table.Column<long>(type: "INTEGER", nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", nullable: true),
                    AddedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PictureAssets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FramePictures_FrameId",
                table: "FramePictures",
                column: "FrameId");

            migrationBuilder.CreateIndex(
                name: "IX_PictureAssets_Hash",
                table: "PictureAssets",
                column: "Hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FrameOptions");

            migrationBuilder.DropTable(
                name: "FramePictures");

            migrationBuilder.DropTable(
                name: "PictureAssets");

            migrationBuilder.DropColumn(
                name: "PictureKeepOriginals",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "PictureMultiDrop",
                table: "UserSettings");
        }
    }
}
