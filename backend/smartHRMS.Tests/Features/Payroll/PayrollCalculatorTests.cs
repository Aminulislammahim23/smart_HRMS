using smartHRMS.Application.Common.Calendar;
using smartHRMS.Application.Features.Payroll;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;
using Xunit;

namespace smartHRMS.Tests.Features.Payroll;

public class PayrollCalculatorTests
{
    // October 2026 with a Friday/Saturday weekend has 21 working days.
    private static readonly DateOnly Start = new(2026, 10, 1);
    private static readonly DateOnly End = new(2026, 10, 31);
    private static readonly WorkCalendar Calendar = new(new WorkCalendarOptions { WeekendDays = ["Friday", "Saturday"] });

    private static AttendanceSummary FullMonth(decimal unpaid = 0, decimal absent = 0, decimal paid = 0) =>
        new(21, 21, 21 - unpaid - absent - paid, paid, unpaid, absent);

    private static PayrollRecord RecordFrom(FixedAmounts amounts, decimal overtime = 0, decimal bonus = 0, decimal advance = 0, decimal loan = 0, decimal other = 0)
    {
        var record = new PayrollRecord
        {
            BasicSalary = amounts.BasicSalary,
            HouseRent = amounts.HouseRent,
            MedicalAllowance = amounts.MedicalAllowance,
            TransportAllowance = amounts.TransportAllowance,
            OtherAllowance = amounts.OtherAllowance,
            Tax = amounts.Tax,
            LeaveDeduction = amounts.LeaveDeduction,
            OvertimeAmount = overtime,
            Bonus = bonus,
            AdvanceDeduction = advance,
            LoanDeduction = loan,
            OtherDeduction = other,
        };
        PayrollCalculator.ApplyTotals(record);
        return record;
    }

    [Fact]
    public void WorkCalendar_October2026_Has21WorkingDays()
    {
        Assert.Equal(21, Calendar.CountWorkingDays(Start, End));
    }

    [Fact]
    public void BasicSalaryOnly_GrossEqualsBasic_NoDeductions()
    {
        var amounts = PayrollCalculator.CalculateFixed(new SalaryInput(30000, 0, 0, 0, 0, 0), FullMonth(), false);
        var record = RecordFrom(amounts);

        Assert.Equal(30000m, record.GrossSalary);
        Assert.Equal(0m, record.TotalDeduction);
        Assert.Equal(30000m, record.NetSalary);
        Assert.Equal(PayrollRecordStatus.Calculated, record.Status);
    }

    [Fact]
    public void BasicPlusAllowances_GrossIsTheSum()
    {
        var amounts = PayrollCalculator.CalculateFixed(new SalaryInput(30000, 15000, 2000, 1500, 500, 0), FullMonth(), false);
        var record = RecordFrom(amounts);

        Assert.Equal(49000m, record.GrossSalary);
        Assert.Equal(49000m, record.NetSalary);
    }

    [Fact]
    public void GrossIncludesOvertimeAndBonus()
    {
        var amounts = PayrollCalculator.CalculateFixed(new SalaryInput(30000, 10000, 0, 0, 0, 0), FullMonth(), false);
        var record = RecordFrom(amounts, overtime: 2500.50m, bonus: 5000m);

        Assert.Equal(47500.50m, record.GrossSalary);
    }

    [Fact]
    public void PaidLeave_DoesNotReduceSalary()
    {
        var amounts = PayrollCalculator.CalculateFixed(new SalaryInput(21000, 0, 0, 0, 0, 0), FullMonth(paid: 3), false);

        Assert.Equal(0m, amounts.LeaveDeduction);
        Assert.Equal(21000m, amounts.BasicSalary);
    }

    [Fact]
    public void UnpaidLeave_IsDeductedAtTheDailyBasicRate()
    {
        // 21000 / 21 working days = 1000 per day.
        var amounts = PayrollCalculator.CalculateFixed(new SalaryInput(21000, 5000, 0, 0, 0, 0), FullMonth(unpaid: 2), false);
        var record = RecordFrom(amounts);

        Assert.Equal(2000m, amounts.LeaveDeduction);
        Assert.Equal(26000m, record.GrossSalary);
        Assert.Equal(24000m, record.NetSalary);
    }

    [Fact]
    public void RecordedAbsences_AreDeductedOnlyWhenEnabled()
    {
        var salary = new SalaryInput(21000, 0, 0, 0, 0, 0);

        Assert.Equal(0m, PayrollCalculator.CalculateFixed(salary, FullMonth(absent: 2), deductRecordedAbsences: false).LeaveDeduction);
        Assert.Equal(2000m, PayrollCalculator.CalculateFixed(salary, FullMonth(absent: 2), deductRecordedAbsences: true).LeaveDeduction);
    }

    [Fact]
    public void MultipleDeductions_AreAllSummed()
    {
        var amounts = PayrollCalculator.CalculateFixed(new SalaryInput(21000, 9000, 0, 0, 0, 1500), FullMonth(unpaid: 1), false);
        var record = RecordFrom(amounts, advance: 2000, loan: 1000, other: 250.25m);

        // Tax 1500 + leave 1000 + advance 2000 + loan 1000 + other 250.25
        Assert.Equal(5750.25m, record.TotalDeduction);
        Assert.Equal(30000m, record.GrossSalary);
        Assert.Equal(24249.75m, record.NetSalary);
    }

    [Fact]
    public void DecimalAmounts_AreRoundedToTwoPlaces_HalfAwayFromZero()
    {
        // 10000 / 21 = 476.190476... per day; 1 unpaid day -> 476.19
        var amounts = PayrollCalculator.CalculateFixed(new SalaryInput(10000, 0, 0, 0, 0, 0), FullMonth(unpaid: 1), false);
        Assert.Equal(476.19m, amounts.LeaveDeduction);

        var halves = new PayrollRecord { BasicSalary = 0.005m, Bonus = 0.005m };
        PayrollCalculator.ApplyTotals(halves);
        Assert.Equal(0.01m, halves.GrossSalary);
    }

    [Fact]
    public void NegativeNet_IsFlaggedForReview()
    {
        var amounts = PayrollCalculator.CalculateFixed(new SalaryInput(10000, 0, 0, 0, 0, 0), FullMonth(), false);
        var record = RecordFrom(amounts, loan: 15000);

        Assert.Equal(-5000m, record.NetSalary);
        Assert.Equal(PayrollRecordStatus.NeedsReview, record.Status);
    }

    [Fact]
    public void JoinerMidPeriod_IsProrated_ByEmployedWorkingDays()
    {
        // Employed for 7 of 21 working days -> one third of everything.
        var summary = new AttendanceSummary(21, 7, 7, 0, 0, 0);
        var amounts = PayrollCalculator.CalculateFixed(new SalaryInput(21000, 6000, 0, 0, 0, 3000), summary, false);

        Assert.Equal(7000m, amounts.BasicSalary);
        Assert.Equal(2000m, amounts.HouseRent);
        Assert.Equal(1000m, amounts.Tax);
    }

    [Fact]
    public void LeaveDeduction_NeverExceedsTheProratedBasic()
    {
        var summary = new AttendanceSummary(21, 2, 0, 0, 5, 0);
        var amounts = PayrollCalculator.CalculateFixed(new SalaryInput(21000, 0, 0, 0, 0, 0), summary, false);

        Assert.Equal(2000m, amounts.BasicSalary);
        Assert.Equal(2000m, amounts.LeaveDeduction);
    }

    [Fact]
    public void Summarize_ApprovedLeaveWins_HalfDaysCountHalf_WeekendsIgnored()
    {
        var attendance = new Dictionary<DateOnly, AttendanceStatus>
        {
            [new DateOnly(2026, 10, 1)] = AttendanceStatus.Present,
            [new DateOnly(2026, 10, 4)] = AttendanceStatus.Late,
            [new DateOnly(2026, 10, 5)] = AttendanceStatus.HalfDay,
            [new DateOnly(2026, 10, 6)] = AttendanceStatus.Absent,
            [new DateOnly(2026, 10, 7)] = AttendanceStatus.Present, // covered by approved leave below
            [new DateOnly(2026, 10, 2)] = AttendanceStatus.Present, // Friday: not a working day
        };
        var leave = new List<LeaveRequest>
        {
            new() { LeaveType = LeaveType.Unpaid, StartDate = new DateOnly(2026, 10, 7), EndDate = new DateOnly(2026, 10, 8), Status = LeaveStatus.Approved },
            new() { LeaveType = LeaveType.Sick, StartDate = new DateOnly(2026, 10, 11), EndDate = new DateOnly(2026, 10, 11), Status = LeaveStatus.Approved },
        };

        var summary = PayrollCalculator.Summarize(Calendar, Start, End, new DateOnly(2020, 1, 1), attendance, leave);

        Assert.Equal(21, summary.PeriodWorkingDays);
        Assert.Equal(21, summary.EmployedWorkingDays);
        Assert.Equal(2.5m, summary.PresentDays);
        Assert.Equal(1.5m, summary.AbsentDays);
        Assert.Equal(2m, summary.UnpaidLeaveDays);
        Assert.Equal(1m, summary.PaidLeaveDays);
    }

    [Fact]
    public void Summarize_JoinerCountsOnlyDaysFromJoining()
    {
        var summary = PayrollCalculator.Summarize(Calendar, Start, End, new DateOnly(2026, 10, 25), new Dictionary<DateOnly, AttendanceStatus>(), []);

        // 25 (Sun) .. 29 (Thu) = 5 working days; 30/31 are Fri/Sat.
        Assert.Equal(5, summary.EmployedWorkingDays);
    }
}
