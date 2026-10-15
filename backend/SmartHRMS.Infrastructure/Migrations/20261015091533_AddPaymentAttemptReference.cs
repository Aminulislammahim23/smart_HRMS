using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace smartHRMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentAttemptReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Reference",
                table: "PaymentStatusHistories",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Reference",
                table: "PaymentStatusHistories");
        }
    }
}
