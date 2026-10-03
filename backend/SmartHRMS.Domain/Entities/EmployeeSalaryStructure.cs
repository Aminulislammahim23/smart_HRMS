using smartHRMS.Domain.Common;

namespace smartHRMS.Domain.Entities;

/// <summary>
/// An employee's fixed monthly allowances and tax, used by payroll. The basic salary itself stays on
/// <see cref="Employee.BasicSalary"/>, so it is stored in one place only.
/// </summary>
public class EmployeeSalaryStructure : EmployeeOwnedEntity
{
    public decimal HouseRent { get; set; }

    public decimal MedicalAllowance { get; set; }

    public decimal TransportAllowance { get; set; }

    public decimal OtherAllowance { get; set; }

    /// <summary>Fixed monthly tax withheld (no tax-slab calculation).</summary>
    public decimal MonthlyTax { get; set; }
}
