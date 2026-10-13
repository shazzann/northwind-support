using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportDesk.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketSlaWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SlaWindowMinutes",
                table: "Tickets",
                type: "int",
                nullable: true);

            // Tickets that already have a due date were given it at creation, so their window is
            // the time between the two.
            migrationBuilder.Sql(
                """
                UPDATE [Tickets]
                SET [SlaWindowMinutes] = DATEDIFF(minute, [CreatedAtUtc], [DueAtUtc])
                WHERE [DueAtUtc] IS NOT NULL AND [DueAtUtc] > [CreatedAtUtc];
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Tickets_SlaWindowMinutes",
                table: "Tickets",
                sql: "[SlaWindowMinutes] IS NULL OR [SlaWindowMinutes] > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Tickets_SlaWindowMinutes",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SlaWindowMinutes",
                table: "Tickets");
        }
    }
}
