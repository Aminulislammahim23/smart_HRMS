using smartHRMS.Application.Common.Calendar;
using smartHRMS.Application.Common.Money;
using smartHRMS.Application.Features.Leaves;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Application.Features.Payroll;

/// <summary>An employee's fixed monthly salary as configured (before proration).</summary>
public sealed record SalaryInput(decimal BasicSalary, decimal HouseRent, decimal MedicalAllowance, decimal TransportAllowance, decimal OtherAllowance, decimal MonthlyTax, decimal MonthlyProvidentFund = 0);

/// <summary>Day counts for one employee in one period. Only working days are counted; half days count 0.5.</summary>
public sealed record AttendanceSummary(int PeriodWorkingDays, int EmployedWorkingDays, decimal PresentDays, decimal PaidLeaveDays, decimal UnpaidLeaveDays, decimal AbsentDays);

/// <summary>The calculated (not manually entered) amounts of a payroll record.</summary>
public sealed record FixedAmounts(decimal BasicSalary, decimal HouseRent, decimal MedicalAllowance, decimal TransportAllowance, decimal OtherAllowance, decimal Tax, decimal LeaveDeduction, decimal ProvidentFund = 0);

/// <summary>
/// Pure payroll arithmetic (decimal only, no database), so every formula can be unit tested.
///
/// Proration: an employee who joined during the period is paid for the working days they were employed:
///   factor = employed working days / period working days; basic, allowances and tax are multiplied by it.
/// Leave deduction: daily rate = monthly basic / period working days;
///   deduction = daily rate × (approved unpaid leave days [+ recorded absent days when enabled]), never more than the
///   prorated basic.
/// Totals:
///   GrossSalary    = Basic + HouseRent + Medical + Transport + OtherAllowance + Overtime + Bonus
///   TotalDeduction = Tax + ProvidentFund + LeaveDeduction + Advance + Loan + OtherDeduction
///   NetSalary      = GrossSalary − TotalDeduction
/// Every amount is rounded to 2 decimals (half away from zero) when it is produced.
/// </summary>
public static class PayrollCalculator
{
    public static FixedAmounts CalculateFixed(SalaryInput salary, AttendanceSummary attendance, bool deductRecordedAbsences)
    {
        if (attendance.PeriodWorkingDays <= 0)
        {
            throw new ArgumentException("The period must contain at least one working day.", nameof(attendance));
        }

        var factor = Math.Min(1m, (decimal)attendance.EmployedWorkingDays / attendance.PeriodWorkingDays);
        var basic = Round(salary.BasicSalary * factor);

        var dailyRate = salary.BasicSalary / attendance.PeriodWorkingDays;
        var deductibleDays = attendance.UnpaidLeaveDays + (deductRecordedAbsences ? attendance.AbsentDays : 0m);
        var leaveDeduction = Math.Min(Round(dailyRate * deductibleDays), basic);

        return new FixedAmounts(
            basic,
            Round(salary.HouseRent * factor),
            Round(salary.MedicalAllowance * factor),
            Round(salary.TransportAllowance * factor),
            Round(salary.OtherAllowance * factor),
            Round(salary.MonthlyTax * factor),
            leaveDeduction,
            Round(salary.MonthlyProvidentFund * factor));
    }

    /// <summary>Recomputes gross, total deduction and net, and flags a negative net salary for review.</summary>
    public static void ApplyTotals(PayrollRecord record)
    {
        record.GrossSalary = Round(record.BasicSalary + record.HouseRent + record.MedicalAllowance + record.TransportAllowance
            + record.OtherAllowance + record.OvertimeAmount + record.Bonus);
        record.TotalDeduction = Round(record.Tax + record.ProvidentFund + record.LeaveDeduction + record.AdvanceDeduction + record.LoanDeduction + record.OtherDeduction);
        record.NetSalary = Round(record.GrossSalary - record.TotalDeduction);
        record.Status = record.NetSalary < 0 ? PayrollRecordStatus.NeedsReview : PayrollRecordStatus.Calculated;
    }

    /// <summary>
    /// Counts each working day from <paramref name="employedFrom"/> to <paramref name="to"/> once, in this order:
    /// approved leave (paid or unpaid) wins; otherwise the attendance record decides — Present/Late = present,
    /// HalfDay = ½ present + ½ absent, Absent = absent, Leave (recorded by HR without a request) = paid leave.
    /// Days with no record are not counted as anything.
    /// </summary>
    public static AttendanceSummary Summarize(
        WorkCalendar calendar,
        DateOnly periodStart,
        DateOnly periodEnd,
        DateOnly employedFrom,
        IReadOnlyDictionary<DateOnly, AttendanceStatus> attendanceByDate,
        IReadOnlyCollection<LeaveRequest> approvedLeave)
    {
        var from = employedFrom > periodStart ? employedFrom : periodStart;
        decimal present = 0, paid = 0, unpaid = 0, absent = 0;
        var employedDays = 0;

        foreach (var day in calendar.WorkingDays(from, periodEnd))
        {
            employedDays++;
            var leave = approvedLeave.FirstOrDefault(l => l.StartDate <= day && l.EndDate >= day);
            if (leave is not null)
            {
                if (LeaveTypeRules.IsPaid(leave.LeaveType))
                {
                    paid++;
                }
                else
                {
                    unpaid++;
                }

                continue;
            }

            if (!attendanceByDate.TryGetValue(day, out var status))
            {
                continue;
            }

            switch (status)
            {
                case AttendanceStatus.Present:
                case AttendanceStatus.Late:
                    present++;
                    break;
                case AttendanceStatus.HalfDay:
                    present += 0.5m;
                    absent += 0.5m;
                    break;
                case AttendanceStatus.Absent:
                    absent++;
                    break;
                case AttendanceStatus.Leave:
                    paid++;
                    break;
            }
        }

        return new AttendanceSummary(calendar.CountWorkingDays(periodStart, periodEnd), employedDays, present, paid, unpaid, absent);
    }

    private static decimal Round(decimal amount) => Money.Round(amount);
}
