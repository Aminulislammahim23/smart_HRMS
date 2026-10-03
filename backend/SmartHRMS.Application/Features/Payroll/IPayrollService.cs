using smartHRMS.Application.Features.Payroll.Dtos;

namespace smartHRMS.Application.Features.Payroll;

public interface IPayrollService
{
    Task<List<PayrollPeriodDto>> GetPeriodsAsync(PayrollPeriodQueryDto query, CancellationToken cancellationToken);

    Task<PayrollPeriodDto> GetPeriodAsync(Guid id, CancellationToken cancellationToken);

    Task<PayrollPeriodDto> CreatePeriodAsync(CreatePayrollPeriodDto dto, CancellationToken cancellationToken);

    Task<PayrollPeriodDto> UpdatePeriodAsync(Guid id, UpdatePayrollPeriodDto dto, CancellationToken cancellationToken);

    Task DeletePeriodAsync(Guid id, CancellationToken cancellationToken);

    Task<PayrollCalculationResultDto> CalculateAsync(Guid periodId, CancellationToken cancellationToken);

    Task<List<PayrollRecordDto>> GetRecordsAsync(Guid periodId, PayrollRecordQueryDto query, CancellationToken cancellationToken);

    Task<PayrollRecordDto> GetRecordAsync(Guid id, CancellationToken cancellationToken);

    Task<PayrollRecordDto> UpdateRecordAsync(Guid id, UpdatePayrollRecordDto dto, CancellationToken cancellationToken);

    Task<PayrollPeriodDto> SubmitAsync(Guid periodId, CancellationToken cancellationToken);

    Task<PayrollPeriodDto> ApproveAsync(Guid periodId, CancellationToken cancellationToken);

    /// <summary>Records payment of every unpaid payslip of an Approved payroll; the payroll becomes Paid.</summary>
    Task<PayrollPeriodDto> MarkPaidAsync(Guid periodId, RecordPaymentDto dto, CancellationToken cancellationToken);

    Task<PayrollPeriodDto> CancelAsync(Guid periodId, CancelPayrollDto dto, CancellationToken cancellationToken);

    Task<List<PayrollRecordDto>> GetEmployeeHistoryAsync(Guid employeeId, CancellationToken cancellationToken);
}
