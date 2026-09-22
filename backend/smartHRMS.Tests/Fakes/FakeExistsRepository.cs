using smartHRMS.Application.Interfaces;

namespace smartHRMS.Tests.Fakes;

public class FakeDepartmentRepository : IDepartmentRepository
{
    public bool ShouldExist { get; set; } = true;

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(ShouldExist);
    }
}

public class FakeDesignationRepository : IDesignationRepository
{
    public bool ShouldExist { get; set; } = true;

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(ShouldExist);
    }
}
