using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace smartHRMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeDocumentDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentName",
                table: "EmployeeDocuments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // Documents uploaded before Day 12 have no title: use their file name so none is left blank.
            migrationBuilder.Sql("UPDATE [EmployeeDocuments] SET [DocumentName] = LEFT([FileName], 200) WHERE [DocumentName] = N''");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiryDate",
                table: "EmployeeDocuments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IssueDate",
                table: "EmployeeDocuments",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentName",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "ExpiryDate",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "IssueDate",
                table: "EmployeeDocuments");
        }
    }
}
