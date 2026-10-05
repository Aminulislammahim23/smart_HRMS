using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace smartHRMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollFinalizationAndPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Payslips",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentReference",
                table: "Payslips",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinalizedAt",
                table: "PayrollPeriods",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinalizedByUserId",
                table: "PayrollPeriods",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "PayrollPeriods",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PaymentBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PayrollPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TotalEmployees = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentBatches", x => x.Id);
                    table.CheckConstraint("CK_PaymentBatches_Totals", "[TotalEmployees] >= 0 AND [TotalAmount] >= 0");
                    table.ForeignKey(
                        name: "FK_PaymentBatches_ApplicationUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentBatches_PayrollPeriods_PayrollPeriodId",
                        column: x => x.PayrollPeriodId,
                        principalTable: "PayrollPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentBatchItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PaymentReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentBatchItems", x => x.Id);
                    table.CheckConstraint("CK_PaymentBatchItems_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_PaymentBatchItems_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentBatchItems_PaymentBatches_PaymentBatchId",
                        column: x => x.PaymentBatchId,
                        principalTable: "PaymentBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentBatchItems_PayrollRecords_PayrollRecordId",
                        column: x => x.PayrollRecordId,
                        principalTable: "PayrollRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentBatchItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TransactionReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactions", x => x.Id);
                    table.CheckConstraint("CK_PaymentTransactions_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_PaymentTransactions_PaidHasDate", "[Status] <> 'Paid' OR ([PaymentDate] IS NOT NULL AND [ProcessedAt] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_PaymentBatchItems_PaymentBatchItemId",
                        column: x => x.PaymentBatchItemId,
                        principalTable: "PaymentBatchItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_PaymentBatches_PaymentBatchId",
                        column: x => x.PaymentBatchId,
                        principalTable: "PaymentBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NewStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentStatusHistories_ApplicationUsers_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentStatusHistories_PaymentTransactions_PaymentTransactionId",
                        column: x => x.PaymentTransactionId,
                        principalTable: "PaymentTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriods_FinalizedByUserId",
                table: "PayrollPeriods",
                column: "FinalizedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentBatches_BatchNumber",
                table: "PaymentBatches",
                column: "BatchNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentBatches_CreatedByUserId",
                table: "PaymentBatches",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentBatches_PayrollPeriodId_Open",
                table: "PaymentBatches",
                column: "PayrollPeriodId",
                unique: true,
                filter: "[Status] <> 'Paid' AND [Status] <> 'Cancelled'");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentBatches_Status",
                table: "PaymentBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentBatchItems_EmployeeId",
                table: "PaymentBatchItems",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentBatchItems_PaymentBatchId_EmployeeId",
                table: "PaymentBatchItems",
                columns: new[] { "PaymentBatchId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentBatchItems_PayrollRecordId_Active",
                table: "PaymentBatchItems",
                column: "PayrollRecordId",
                unique: true,
                filter: "[Status] <> 'Cancelled'");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentStatusHistories_ChangedByUserId",
                table: "PaymentStatusHistories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentStatusHistories_PaymentTransactionId_CreatedAt",
                table: "PaymentStatusHistories",
                columns: new[] { "PaymentTransactionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_EmployeeId_Status",
                table: "PaymentTransactions",
                columns: new[] { "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_PaymentBatchId",
                table: "PaymentTransactions",
                column: "PaymentBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_PaymentBatchItemId",
                table: "PaymentTransactions",
                column: "PaymentBatchItemId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollPeriods_ApplicationUsers_FinalizedByUserId",
                table: "PayrollPeriods",
                column: "FinalizedByUserId",
                principalTable: "ApplicationUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PayrollPeriods_ApplicationUsers_FinalizedByUserId",
                table: "PayrollPeriods");

            migrationBuilder.DropTable(
                name: "PaymentStatusHistories");

            migrationBuilder.DropTable(
                name: "PaymentTransactions");

            migrationBuilder.DropTable(
                name: "PaymentBatchItems");

            migrationBuilder.DropTable(
                name: "PaymentBatches");

            migrationBuilder.DropIndex(
                name: "IX_PayrollPeriods_FinalizedByUserId",
                table: "PayrollPeriods");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "PaymentReference",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "FinalizedAt",
                table: "PayrollPeriods");

            migrationBuilder.DropColumn(
                name: "FinalizedByUserId",
                table: "PayrollPeriods");

            migrationBuilder.DropColumn(
                name: "IsLocked",
                table: "PayrollPeriods");
        }
    }
}
