using smartHRMS.Application.Features.Leaves.Dtos;

namespace smartHRMS.Application.Features.Leaves;

public interface ILeaveService
{
    List<LeaveTypeDto> GetLeaveTypes();

    Task<List<LeaveRequestDto>> GetAllAsync(LeaveQueryDto query, CancellationToken cancellationToken);

    Task<LeaveRequestDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<LeaveRequestDto> CreateAsync(CreateLeaveRequestDto dto, CancellationToken cancellationToken);

    Task<LeaveRequestDto> ApproveAsync(Guid id, ReviewLeaveRequestDto dto, CancellationToken cancellationToken);

    Task<LeaveRequestDto> RejectAsync(Guid id, ReviewLeaveRequestDto dto, CancellationToken cancellationToken);

    Task<LeaveRequestDto> CancelAsync(Guid id, ReviewLeaveRequestDto dto, CancellationToken cancellationToken);
}
