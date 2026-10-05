using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Features.Payroll;

/// <summary>
/// The single place where payslips are issued and their payment outcome recorded. A payslip only ever exists for
/// Approved, Finalized or Paid payroll: its amounts are the record's, which is locked from approval on, so the payslip
/// is the approved snapshot. Payment itself is managed by payment batches (Day 18); the payslip mirrors the result.
/// </summary>
internal static class PayslipIssuer
{
    public static readonly IReadOnlyCollection<PayrollPeriodStatus> IssuableStatuses =
        new[] { PayrollPeriodStatus.Approved, PayrollPeriodStatus.Finalized, PayrollPeriodStatus.Paid };

    public static string NumberFor(PayrollPeriod period, PayrollRecord record) => $"PS-{period.StartDate:yyyyMMdd}-{record.EmployeeCode}";

    /// <summary>Creates payslips for the period's records that don't have one yet; returns the new ones (not yet added).</summary>
    public static List<Payslip> IssueMissing(PayrollPeriod period, Guid? userId, DateTime now)
    {
        if (!IssuableStatuses.Contains(period.Status))
        {
            throw new ConflictException($"Payslips are issued only for approved payroll; this payroll is {period.Status}.");
        }

        var issued = new List<Payslip>();
        foreach (var record in period.Records.Where(r => r.Payslip is null))
        {
            var payslip = new Payslip
            {
                PayslipNumber = NumberFor(period, record),
                PayrollRecordId = record.Id,
                PayrollRecord = record,
                PayrollPeriodId = period.Id,
                EmployeeId = record.EmployeeId,
                GeneratedAt = now,
                GeneratedByUserId = userId,
                PaymentStatus = PaymentStatus.Unpaid,
            };

            // A period already marked Paid before payslips existed (pre-Day 17 data) gets paid payslips.
            if (period.Status == PayrollPeriodStatus.Paid)
            {
                payslip.PaymentStatus = PaymentStatus.Paid;
                payslip.PaymentDate = period.PaidAt is { } paidAt ? DateOnly.FromDateTime(paidAt) : DateOnly.FromDateTime(now);
                payslip.PaidAt = period.PaidAt ?? now;
                payslip.PaidByUserId = period.PaidByUserId;
            }

            record.Payslip = payslip;
            issued.Add(payslip);
        }

        return issued;
    }

    /// <summary>
    /// The payment date: the requested one, or today's office date. It can't be in the future or before the period
    /// started.
    /// </summary>
    public static DateOnly PaymentDate(DateOnly? requested, PayrollPeriod period, DateOnly today)
    {
        var date = requested ?? today;
        if (date > today)
        {
            throw new BadRequestException($"The payment date can't be in the future (today is {today:yyyy-MM-dd}).");
        }

        if (date < period.StartDate)
        {
            throw new BadRequestException($"The payment date can't be before the payroll period starts ({period.StartDate:yyyy-MM-dd}).");
        }

        return date;
    }

    /// <summary>Copies a confirmed payment onto the employee's payslip.</summary>
    public static void MarkPaid(Payslip payslip, DateOnly paymentDate, PaymentMethod method, string? reference, Guid userId, DateTime now)
    {
        payslip.PaymentStatus = PaymentStatus.Paid;
        payslip.PaymentDate = paymentDate;
        payslip.PaymentMethod = method;
        payslip.PaymentReference = reference;
        payslip.PaidAt = now;
        payslip.PaidByUserId = userId;
        payslip.UpdatedAt = now;
    }

    /// <summary>A payslip needs paying when its net salary is above zero.</summary>
    public static bool IsPayable(PayrollRecord record) => record.NetSalary > 0;

    /// <summary>
    /// Once every payable payslip of a Finalized period is paid, the period (and its records) become Paid. Records with
    /// a zero net salary have nothing to pay and don't hold the period open.
    /// </summary>
    public static bool CompletePeriodIfAllPaid(PayrollPeriod period, Guid userId, DateTime now)
    {
        if (period.Status != PayrollPeriodStatus.Finalized || period.Records.Count == 0
            || period.Records.Where(IsPayable).Any(r => r.Payslip?.PaymentStatus != PaymentStatus.Paid))
        {
            return false;
        }

        period.Status = PayrollPeriodStatus.Paid;
        period.PaidAt = now;
        period.PaidByUserId = userId;
        period.UpdatedAt = now;
        foreach (var record in period.Records)
        {
            record.Status = PayrollRecordStatus.Paid;
            record.UpdatedAt = now;
        }

        return true;
    }
}
