using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StuRoom.Migrations
{
    /// <inheritdoc />
    public partial class AddRemindersSentFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DueReminderSent",
                table: "Invoices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ExpiryReminderSent",
                table: "Contracts",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DueReminderSent",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ExpiryReminderSent",
                table: "Contracts");
        }
    }
}
