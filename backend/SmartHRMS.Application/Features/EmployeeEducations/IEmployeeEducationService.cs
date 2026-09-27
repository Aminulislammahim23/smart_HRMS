using smartHRMS.Application.Features.EmployeeEducations.Dtos;
using smartHRMS.Application.Features.Employees;

namespace smartHRMS.Application.Features.EmployeeEducations;

public interface IEmployeeEducationService : IEmployeeOwnedRecordService<CreateEmployeeEducationDto, EmployeeEducationDto>
{
}
