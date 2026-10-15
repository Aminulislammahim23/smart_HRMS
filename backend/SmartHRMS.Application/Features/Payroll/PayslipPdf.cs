using System.Globalization;
using smartHRMS.Application.Common.Export;
using smartHRMS.Application.Features.Payroll.Dtos;

namespace smartHRMS.Application.Features.Payroll;

/// <summary>
/// Lays out a payslip as a one-page A4 PDF (Day 20). It renders the same <see cref="PayslipDto"/> as the payslip
/// page, so the PDF and the screen can never disagree: nothing is recalculated. Only what the payslip page shows is
/// included (no bank or identity numbers).
/// </summary>
internal static class PayslipPdf
{
    private const float Left = 40f;
    private const float Right = SimplePdf.PageWidth - 40f;
    private const float Middle = SimplePdf.PageWidth / 2f;

    public static byte[] Render(PayslipDto p)
    {
        var pdf = new SimplePdf($"Payslip {p.PayslipNumber} - {p.EmployeeName}");
        var y = 40f;

        // Header band: company on the left, document title and number on the right.
        pdf.FillRect(Left, y, Right - Left, 64f, 0.94f);
        pdf.TextFit(Left + 14, y + 26, p.CompanyName, 18, Middle - Left - 20, bold: true);
        if (!string.IsNullOrWhiteSpace(p.CompanyAddress))
        {
            pdf.TextFit(Left + 14, y + 44, p.CompanyAddress, 9, Middle - Left - 20, gray: 0.35f);
        }

        pdf.Text(Right - 14, y + 26, "PAYSLIP", 16, bold: true, align: PdfAlign.Right);
        pdf.Text(Right - 14, y + 44, p.PayslipNumber ?? "Preview (not issued)", 10, align: PdfAlign.Right, gray: 0.35f);
        y += 88f;

        // Employee and payroll information, two columns.
        Section(pdf, Left, y, "EMPLOYEE INFORMATION");
        Section(pdf, Middle + 10, y, "PAYROLL INFORMATION");
        y += 18f;
        var employee = new (string, string)[]
        {
            ("Employee ID", p.EmployeeCode),
            ("Employee name", p.EmployeeName),
            ("Department", p.DepartmentName ?? "-"),
            ("Designation", p.DesignationName ?? "-"),
            ("Joining date", Date(p.JoiningDate)),
        };
        var payroll = new (string, string)[]
        {
            ("Payroll month", p.PeriodName),
            ("Pay period", $"{Date(p.PeriodStartDate)} – {Date(p.PeriodEndDate)}"),
            ("Payment date", p.PaymentDate is null ? "Not paid yet" : Date(p.PaymentDate)),
            ("Payroll status", Words(p.PayrollStatus)),
            ("Payment status", p.PaymentStatus),
        };
        for (var i = 0; i < employee.Length; i++)
        {
            Field(pdf, Left, Middle - 10, y, employee[i].Item1, employee[i].Item2);
            Field(pdf, Middle + 10, Right, y, payroll[i].Item1, payroll[i].Item2);
            y += 17f;
        }

        y += 14f;

        // Earnings and deductions side by side, each with its total.
        Section(pdf, Left, y, $"EARNINGS ({p.Currency})");
        Section(pdf, Middle + 10, y, $"DEDUCTIONS ({p.Currency})");
        y += 18f;
        var rows = Math.Max(p.Earnings.Count, p.Deductions.Count);
        for (var i = 0; i < rows; i++)
        {
            if (i < p.Earnings.Count)
            {
                Amount(pdf, Left, Middle - 10, y, p.Earnings[i].Label, p.Earnings[i].Amount);
            }

            if (i < p.Deductions.Count)
            {
                Amount(pdf, Middle + 10, Right, y, p.Deductions[i].Label, p.Deductions[i].Amount);
            }

            y += 17f;
        }

        pdf.Line(Left, y - 9, Middle - 10, y - 9, 0.8f, 0.2f);
        pdf.Line(Middle + 10, y - 9, Right, y - 9, 0.8f, 0.2f);
        Amount(pdf, Left, Middle - 10, y + 4, "Gross salary", p.GrossSalary, bold: true);
        Amount(pdf, Middle + 10, Right, y + 4, "Total deduction", p.TotalDeduction, bold: true);
        y += 30f;

        // Net salary band.
        pdf.FillRect(Left, y, Right - Left, 40f, 0.9f);
        pdf.Text(Left + 14, y + 25, "NET SALARY", 12, bold: true);
        pdf.Text(Right - 14, y + 25, $"{p.Currency} {Money(p.NetSalary)}", 15, bold: true, align: PdfAlign.Right);
        y += 62f;

        // Attendance used by the calculation.
        Section(pdf, Left, y, "ATTENDANCE (DAYS)");
        y += 18f;
        var days = new (string, decimal)[] { ("Working", p.WorkingDays), ("Present", p.PresentDays), ("Paid leave", p.PaidLeaveDays), ("Unpaid leave", p.UnpaidLeaveDays), ("Absent", p.AbsentDays) };
        var column = (Right - Left) / days.Length;
        for (var i = 0; i < days.Length; i++)
        {
            pdf.Text(Left + column * i, y, days[i].Item1, 8.5f, gray: 0.4f);
            pdf.Text(Left + column * i, y + 14, days[i].Item2.ToString("0.##", CultureInfo.InvariantCulture), 11, bold: true);
        }

        y += 40f;
        if (!string.IsNullOrWhiteSpace(p.Remarks))
        {
            pdf.TextFit(Left, y, $"Remarks: {p.Remarks}", 9, Right - Left, gray: 0.3f);
            y += 18f;
        }

        // Footer.
        pdf.Line(Left, y, Right, y);
        y += 16f;
        pdf.Text(Left, y, $"Issued: {(p.GeneratedAt is { } at ? at.ToString("dd MMM yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture) : "not yet (preview)")}", 8.5f, gray: 0.35f);
        if (p.ApprovedBy is not null)
        {
            pdf.Text(Right, y, $"Approved by {p.ApprovedBy}", 8.5f, align: PdfAlign.Right, gray: 0.35f);
        }

        y += 14f;
        pdf.Text(Left, y, "This payslip is generated by SmartHRMS from the finalized payroll and is valid without a signature.", 8.5f, gray: 0.35f);
        if (p.PaymentReference is not null || p.PaymentMethod is not null)
        {
            y += 14f;
            pdf.Text(Left, y, $"Payment: {Words(p.PaymentMethod ?? "-")}{(p.PaymentReference is null ? string.Empty : $", reference {p.PaymentReference}")}", 8.5f, gray: 0.35f);
        }

        return pdf.ToBytes();
    }

    private static void Section(SimplePdf pdf, float x, float y, string title) => pdf.Text(x, y, title, 9, bold: true, gray: 0.3f);

    private static void Field(SimplePdf pdf, float x, float right, float y, string label, string value)
    {
        pdf.Text(x, y, label, 9.5f, gray: 0.4f);
        pdf.TextFit(right, y, value, 9.5f, (right - x) * 0.6f, bold: true, align: PdfAlign.Right);
    }

    private static void Amount(SimplePdf pdf, float x, float right, float y, string label, decimal amount, bool bold = false)
    {
        pdf.Text(x, y, label, 9.5f, bold);
        pdf.Text(right, y, Money(amount), 9.5f, bold, PdfAlign.Right);
    }

    private static string Money(decimal value) => value.ToString("#,##0.00", CultureInfo.InvariantCulture);

    private static string Date(DateOnly? date) => date?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) ?? "-";

    /// <summary>"PendingApproval" → "Pending approval", "BankTransfer" → "Bank transfer".</summary>
    private static string Words(string value)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var ch in value)
        {
            if (char.IsUpper(ch) && builder.Length > 0)
            {
                builder.Append(' ').Append(char.ToLowerInvariant(ch));
            }
            else
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }
}
