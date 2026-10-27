using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniTube.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SearchVideo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SearchText",
                table: "Videos",
                type: "nvarchar(max)",
                maxLength: 6000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SearchTitle",
                table: "Videos",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SearchName",
                table: "Users",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Videos_SearchTitle",
                table: "Videos",
                column: "SearchTitle");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Videos_SearchTitle",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "SearchText",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "SearchTitle",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "SearchName",
                table: "Users");
        }
    }
}
