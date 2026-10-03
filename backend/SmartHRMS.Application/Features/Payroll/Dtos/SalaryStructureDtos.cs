using System.ComponentModel.DataAnnotations;

namespace smartHRMS.Application.Features.Payroll.Dtos;

/// <summary>An employee's monthly salary: basic (from the employee record) plus allowances and tax.</summary>
public class SalaryStructureDto
{
    public Guid EmployeeId { get; set; }

    public string EmployeeCode { get; set; } = string.Empty;

    public string EmployeeName { get; set; } = string.Empty;

    public string? DepartmentName { get; set; }

    public string? DesignationName { get; set; }

    public string EmployeeStatus { get; set; } = string.Empty;

    /// <summary>Null when no basic salary is set: payroll then skips the employee.</summary>
    public decimal? BasicSalary { get; set; }

    public decimal HouseRent { get; set; }

    public decimal MedicalAllowance { get; set; }

    public decimal TransportAllowance { get; set; }

    public decimal OtherAllowance { get; set; }

    public decimal MonthlyTax { get; set; }

    /// <summary>Monthly provident fund contribution (employee share), withheld from salary.</summary>
    public decimal MonthlyProvidentFund { get; set; }

    /// <summary>Basic + all allowances (before tax and deductions).</summary>
    public decimal? MonthlyGross { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public class UpdateSalaryStructureDto
{
    private const string Max = "9999999999999999.99";

    [Required]
    [Range(typeof(decimal), "0", Max, ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true, ErrorMessage = "The BasicSalary field must be 0 or more.")]
    public decimal? BasicSalary { get; set; }

    [Range(typeof(decimal), "0", Max, ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true, ErrorMessage = "The HouseRent field must be 0 or more.")]
    public decimal HouseRent { get; set; }

    [Range(typeof(decimal), "0", Max, ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true, ErrorMessage = "The MedicalAllowance field must be 0 or more.")]
    public decimal MedicalAllowance { get; set; }

    [Range(typeof(decimal), "0", Max, ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true, ErrorMessage = "The TransportAllowance field must be 0 or more.")]
    public decimal TransportAllowance { get; set; }

    [Range(typeof(decimal), "0", Max, ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true, ErrorMessage = "The OtherAllowance field must be 0 or more.")]
    public decimal OtherAllowance { get; set; }

    [Range(typeof(decimal), "0", Max, ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true, ErrorMessage = "The MonthlyTax field must be 0 or more.")]
    public decimal MonthlyTax { get; set; }

    [Range(typeof(decimal), "0", Max, ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true, ErrorMessage = "The MonthlyProvidentFund field must be 0 or more.")]
    public decimal MonthlyProvidentFund { get; set; }
}
