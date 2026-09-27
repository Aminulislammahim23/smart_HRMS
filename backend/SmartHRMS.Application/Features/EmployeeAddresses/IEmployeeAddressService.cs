using smartHRMS.Application.Features.EmployeeAddresses.Dtos;
using smartHRMS.Application.Features.Employees;

namespace smartHRMS.Application.Features.EmployeeAddresses;

public interface IEmployeeAddressService : IEmployeeOwnedRecordService<CreateEmployeeAddressDto, EmployeeAddressDto>
{
}
