using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Interfaces;
using smartHRMS.Domain.Common;

namespace smartHRMS.Application.Features.Employees;

/// <summary>List/get/create/update/delete for one kind of employee profile record (address, contact, ...).</summary>
public interface IEmployeeOwnedRecordService<TSaveDto, TDto>
{
    Task<List<TDto>> GetAllAsync(Guid employeeId, CancellationToken cancellationToken);

    Task<TDto> GetByIdAsync(Guid employeeId, Guid id, CancellationToken cancellationToken);

    Task<TDto> CreateAsync(Guid employeeId, TSaveDto dto, CancellationToken cancellationToken);

    Task<TDto> UpdateAsync(Guid employeeId, Guid id, TSaveDto dto, CancellationToken cancellationToken);

    Task DeleteAsync(Guid employeeId, Guid id, CancellationToken cancellationToken);
}

/// <summary>
/// Shared workflow for employee-owned profile records. It always checks that the employee exists and only ever
/// finds a record through its own employee, so ids can't be swapped to reach another employee's data. Each feature
/// only supplies its validation/copy rules (<see cref="ApplyAsync"/>) and response mapping (<see cref="MapToDto"/>).
/// </summary>
public abstract class EmployeeOwnedRecordService<TEntity, TSaveDto, TDto> : IEmployeeOwnedRecordService<TSaveDto, TDto>
    where TEntity : EmployeeOwnedEntity, new()
{
    protected EmployeeOwnedRecordService(IEmployeeOwnedRepository<TEntity> repository, IEmployeeRepository employeeRepository)
    {
        Repository = repository;
        EmployeeRepository = employeeRepository;
    }

    protected IEmployeeOwnedRepository<TEntity> Repository { get; }

    protected IEmployeeRepository EmployeeRepository { get; }

    /// <summary>Used in error messages, e.g. "Address".</summary>
    protected abstract string RecordName { get; }

    public async Task<List<TDto>> GetAllAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        await EmployeeRepository.EnsureExistsAsync(employeeId, cancellationToken);

        var records = await Repository.GetByEmployeeIdAsync(employeeId, cancellationToken);
        return Order(records).Select(MapToDto).ToList();
    }

    public async Task<TDto> GetByIdAsync(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        await EmployeeRepository.EnsureExistsAsync(employeeId, cancellationToken);
        return MapToDto(await GetOwnedAsync(employeeId, id, cancellationToken));
    }

    public async Task<TDto> CreateAsync(Guid employeeId, TSaveDto dto, CancellationToken cancellationToken)
    {
        await EmployeeRepository.EnsureExistsAsync(employeeId, cancellationToken);

        var record = new TEntity { EmployeeId = employeeId };
        await ApplyAsync(record, dto, cancellationToken);

        await Repository.AddAsync(record, cancellationToken);
        await Repository.SaveChangesAsync(cancellationToken);

        return MapToDto(record);
    }

    public async Task<TDto> UpdateAsync(Guid employeeId, Guid id, TSaveDto dto, CancellationToken cancellationToken)
    {
        await EmployeeRepository.EnsureExistsAsync(employeeId, cancellationToken);

        var record = await GetOwnedAsync(employeeId, id, cancellationToken);
        await ApplyAsync(record, dto, cancellationToken);
        record.UpdatedAt = DateTime.UtcNow;

        await Repository.SaveChangesAsync(cancellationToken);

        return MapToDto(record);
    }

    public async Task DeleteAsync(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        await EmployeeRepository.EnsureExistsAsync(employeeId, cancellationToken);

        var record = await GetOwnedAsync(employeeId, id, cancellationToken);
        Repository.Remove(record);

        await Repository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Validates <paramref name="dto"/> against business rules and copies it onto <paramref name="record"/>.</summary>
    protected abstract Task ApplyAsync(TEntity record, TSaveDto dto, CancellationToken cancellationToken);

    protected abstract TDto MapToDto(TEntity record);

    /// <summary>Display order of the list. Defaults to creation order.</summary>
    protected virtual IEnumerable<TEntity> Order(IEnumerable<TEntity> records) => records;

    private async Task<TEntity> GetOwnedAsync(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        return await Repository.GetByIdAsync(employeeId, id, cancellationToken)
            ?? throw new NotFoundException($"{RecordName} with id '{id}' was not found for employee '{employeeId}'.");
    }
}
