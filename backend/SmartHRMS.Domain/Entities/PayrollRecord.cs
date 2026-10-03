using smartHRMS.Domain.Common;
using smartHRMS.Domain.Enums;

namespace smartHRMS.Domain.Entities;

/// <summary>
/// One employee's salary for one payroll period (at most one per employee and period). Amounts are calculated by the
/// server; the employee's name, code, department and designation are copied at calculation time so an issued payslip
/// never changes when the employee record does.
/// </summary>
public class PayrollRecord : BaseEntity
{
    public Guid PayrollPeriodId { get; set; }

    public PayrollPeriod? PayrollPeriod { get; set; }

    public Guid EmployeeId { get; set; }

    public Employee? Employee { get; set; }

    public string EmployeeCode { get; set; } = string.Empty;

    public string EmployeeName { get; set; } = string.Empty;

    public string? DepartmentName { get; set; }

    public string? DesignationName { get; set; }

    // Earnings
    public decimal BasicSalary { get; set; }

    public decimal HouseRent { get; set; }

    public decimal MedicalAllowance { get; set; }

    public decimal TransportAllowance { get; set; }

    public decimal OtherAllowance { get; set; }

    public decimal OvertimeAmount { get; set; }

    public decimal Bonus { get; set; }

    public decimal GrossSalary { get; set; }

    // Deductions
    public decimal Tax { get; set; }

    public decimal LeaveDeduction { get; set; }

    public decimal ProvidentFund { get; set; }

    public decimal AdvanceDeduction { get; set; }

    public decimal LoanDeduction { get; set; }

    public decimal OtherDeduction { get; set; }

    public decimal TotalDeduction { get; set; }

    public decimal NetSalary { get; set; }

    // Attendance summary (working days only; half days count 0.5)
    public decimal WorkingDays { get; set; }

    public decimal PresentDays { get; set; }

    public decimal PaidLeaveDays { get; set; }

    public decimal UnpaidLeaveDays { get; set; }

    public decimal AbsentDays { get; set; }

    public PayrollRecordStatus Status { get; set; } = PayrollRecordStatus.Calculated;

    public string? Remarks { get; set; }

    /// <summary>Issued once the payroll is approved; null before.</summary>
    public Payslip? Payslip { get; set; }
}
