using smartHRMS.Application.Features.EmployeeExperiences.Dtos;
using smartHRMS.Application.Features.Employees;

namespace smartHRMS.Application.Features.EmployeeExperiences;

public interface IEmployeeExperienceService : IEmployeeOwnedRecordService<CreateEmployeeExperienceDto, EmployeeExperienceDto>
{
}
