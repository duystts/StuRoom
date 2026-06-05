using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StuRoom.Migrations
{
    /// <inheritdoc />
    public partial class AddFavoritesReportsCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminNote",
                table: "RoomReports");

            migrationBuilder.DropColumn(
                name: "Content",
                table: "RoomReports");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "RoomReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "AdminFeedback",
                table: "RoomReports",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "RoomReports",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "HandledAt",
                table: "RoomReports",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminFeedback",
                table: "RoomReports");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "RoomReports");

            migrationBuilder.DropColumn(
                name: "HandledAt",
                table: "RoomReports");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "RoomReports",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "AdminNote",
                table: "RoomReports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Content",
                table: "RoomReports",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
