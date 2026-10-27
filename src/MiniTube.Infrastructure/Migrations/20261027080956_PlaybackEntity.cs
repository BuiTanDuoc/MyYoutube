using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniTube.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlaybackEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DislikeCount",
                table: "Videos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LikeCount",
                table: "Videos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "ViewCount",
                table: "Videos",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "WatchHistoryPaused",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "VideoReactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VideoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoReactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoReactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VideoReactions_Videos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "Videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoViews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VideoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ViewerKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoViews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoViews_Videos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "Videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WatchHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VideoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastPositionSeconds = table.Column<double>(type: "float", nullable: false),
                    Completed = table.Column<bool>(type: "bit", nullable: false),
                    LastWatchedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WatchHistory_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WatchHistory_Videos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "Videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VideoReactions_UserId_Type_CreatedAt",
                table: "VideoReactions",
                columns: new[] { "UserId", "Type", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VideoReactions_UserId_VideoId",
                table: "VideoReactions",
                columns: new[] { "UserId", "VideoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VideoReactions_VideoId",
                table: "VideoReactions",
                column: "VideoId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoViews_VideoId_UserId_CreatedAt",
                table: "VideoViews",
                columns: new[] { "VideoId", "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VideoViews_VideoId_ViewerKey_CreatedAt",
                table: "VideoViews",
                columns: new[] { "VideoId", "ViewerKey", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WatchHistory_UserId_LastWatchedAt",
                table: "WatchHistory",
                columns: new[] { "UserId", "LastWatchedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WatchHistory_UserId_VideoId",
                table: "WatchHistory",
                columns: new[] { "UserId", "VideoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchHistory_VideoId",
                table: "WatchHistory",
                column: "VideoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoReactions");

            migrationBuilder.DropTable(
                name: "VideoViews");

            migrationBuilder.DropTable(
                name: "WatchHistory");

            migrationBuilder.DropColumn(
                name: "DislikeCount",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "LikeCount",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "ViewCount",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "WatchHistoryPaused",
                table: "Users");
        }
    }
}
