using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace smartHRMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayslipsAndProvidentFund : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollRecords_NonNegative",
                table: "PayrollRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeSalaryStructures_NonNegative",
                table: "EmployeeSalaryStructures");

            migrationBuilder.AddColumn<decimal>(
                name: "ProvidentFund",
                table: "PayrollRecords",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyProvidentFund",
                table: "EmployeeSalaryStructures",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "Payslips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayslipNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PayrollRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GeneratedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaymentStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Unpaid"),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaidByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payslips", x => x.Id);
                    table.CheckConstraint("CK_Payslips_PaidHasDate", "([PaymentStatus] = 'Paid' AND [PaymentDate] IS NOT NULL) OR ([PaymentStatus] = 'Unpaid' AND [PaymentDate] IS NULL)");
                    table.ForeignKey(
                        name: "FK_Payslips_ApplicationUsers_GeneratedByUserId",
                        column: x => x.GeneratedByUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payslips_ApplicationUsers_PaidByUserId",
                        column: x => x.PaidByUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payslips_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payslips_PayrollPeriods_PayrollPeriodId",
                        column: x => x.PayrollPeriodId,
                        principalTable: "PayrollPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payslips_PayrollRecords_PayrollRecordId",
                        column: x => x.PayrollRecordId,
                        principalTable: "PayrollRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollRecords_NonNegative",
                table: "PayrollRecords",
                sql: "[BasicSalary] >= 0 AND [HouseRent] >= 0 AND [MedicalAllowance] >= 0 AND [TransportAllowance] >= 0 AND [OtherAllowance] >= 0 AND [OvertimeAmount] >= 0 AND [Bonus] >= 0 AND [Tax] >= 0 AND [LeaveDeduction] >= 0 AND [AdvanceDeduction] >= 0 AND [LoanDeduction] >= 0 AND [OtherDeduction] >= 0 AND [ProvidentFund] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeSalaryStructures_NonNegative",
                table: "EmployeeSalaryStructures",
                sql: "[HouseRent] >= 0 AND [MedicalAllowance] >= 0 AND [TransportAllowance] >= 0 AND [OtherAllowance] >= 0 AND [MonthlyTax] >= 0 AND [MonthlyProvidentFund] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Payslips_EmployeeId_PayrollPeriodId",
                table: "Payslips",
                columns: new[] { "EmployeeId", "PayrollPeriodId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payslips_GeneratedByUserId",
                table: "Payslips",
                column: "GeneratedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Payslips_PaidByUserId",
                table: "Payslips",
                column: "PaidByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Payslips_PaymentStatus",
                table: "Payslips",
                column: "PaymentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Payslips_PayrollPeriodId",
                table: "Payslips",
                column: "PayrollPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_Payslips_PayrollRecordId",
                table: "Payslips",
                column: "PayrollRecordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payslips_PayslipNumber",
                table: "Payslips",
                column: "PayslipNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Payslips");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollRecords_NonNegative",
                table: "PayrollRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeSalaryStructures_NonNegative",
                table: "EmployeeSalaryStructures");

            migrationBuilder.DropColumn(
                name: "ProvidentFund",
                table: "PayrollRecords");

            migrationBuilder.DropColumn(
                name: "MonthlyProvidentFund",
                table: "EmployeeSalaryStructures");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollRecords_NonNegative",
                table: "PayrollRecords",
                sql: "[BasicSalary] >= 0 AND [HouseRent] >= 0 AND [MedicalAllowance] >= 0 AND [TransportAllowance] >= 0 AND [OtherAllowance] >= 0 AND [OvertimeAmount] >= 0 AND [Bonus] >= 0 AND [Tax] >= 0 AND [LeaveDeduction] >= 0 AND [AdvanceDeduction] >= 0 AND [LoanDeduction] >= 0 AND [OtherDeduction] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeSalaryStructures_NonNegative",
                table: "EmployeeSalaryStructures",
                sql: "[HouseRent] >= 0 AND [MedicalAllowance] >= 0 AND [TransportAllowance] >= 0 AND [OtherAllowance] >= 0 AND [MonthlyTax] >= 0");
        }
    }
}
