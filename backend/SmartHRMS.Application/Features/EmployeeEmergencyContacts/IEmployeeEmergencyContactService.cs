using smartHRMS.Application.Features.EmployeeEmergencyContacts.Dtos;
using smartHRMS.Application.Features.Employees;

namespace smartHRMS.Application.Features.EmployeeEmergencyContacts;

public interface IEmployeeEmergencyContactService
    : IEmployeeOwnedRecordService<CreateEmployeeEmergencyContactDto, EmployeeEmergencyContactDto>
{
}
