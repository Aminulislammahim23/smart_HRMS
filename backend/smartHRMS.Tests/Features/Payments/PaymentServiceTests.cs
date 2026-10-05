using smartHRMS.Application.Common.Calendar;
using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Features.Attendances;
using smartHRMS.Application.Features.Payments;
using smartHRMS.Application.Features.Payments.Dtos;
using smartHRMS.Application.Features.Payroll;
using smartHRMS.Application.Features.Payroll.Dtos;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;
using smartHRMS.Tests.Fakes;
using Xunit;

namespace smartHRMS.Tests.Features.Payments;

public class PaymentServiceTests
{
    private static readonly DateOnly OctStart = new(2026, 10, 1);
    private static readonly DateOnly OctEnd = new(2026, 10, 31);

    private sealed class Setup
    {
        public FakeEmployeeRepository Employees { get; } = new();
        public FakePayrollRepository Payroll { get; }
        public FakePaymentRepository Payments { get; } = new();
        public FakeAuditLogger Audit { get; } = new();
        public FakeCurrentUser User { get; } = FakeCurrentUser.As(UserRole.HR);
        public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 5, 6, 0, 0, TimeSpan.Zero)); // office date 2026-11-05
        public PayrollService Payrolls { get; }
        public PaymentService Service { get; }
        public Employee Alice { get; }
        public Employee Bob { get; }

        public Setup()
        {
            Payroll = new FakePayrollRepository(Employees);
            Alice = Add("EMP-001", "Alice", 21000, new EmployeeSalaryStructure { HouseRent = 10500, MonthlyTax = 1000 }); // net 30500
            Bob = Add("EMP-002", "Bob", 42000, null); // net 42000
            var users = new FakeUserRepository();
            var clock = new AttendanceClock(Clock, new AttendanceOptions());
            Payrolls = new PayrollService(Payroll, Employees, new FakeAttendanceRepository(Employees), new FakeLeaveRequestRepository(Employees), users, Audit, User,
                new WorkCalendar(new WorkCalendarOptions { WeekendDays = ["Friday", "Saturday"] }), new PayrollOptions(), clock);
            Service = new PaymentService(Payments, Payroll, users, Audit, User, clock);
        }

        private Employee Add(string code, string name, decimal basic, EmployeeSalaryStructure? structure)
        {
            var employee = new Employee
            {
                EmployeeCode = code, FirstName = name, LastName = "Test", Status = EmployeeStatus.Active, JoiningDate = new DateTime(2020, 1, 1),
                BasicSalary = basic, Department = new Department { Name = "Ops" }, Designation = new Designation { Name = "Clerk" },
            };
            if (structure is not null)
            {
                structure.EmployeeId = employee.Id;
                employee.SalaryStructure = structure;
            }

            Employees.Employees.Add(employee);
            return employee;
        }

        /// <summary>October 2026 created, calculated, submitted (HR) and approved (Admin); returns the period id.</summary>
        public async Task<Guid> ApprovedAsync()
        {
            User.SignInAs(UserRole.HR);
            var period = await Payrolls.CreatePeriodAsync(new CreatePayrollPeriodDto { StartDate = OctStart, EndDate = OctEnd }, CancellationToken.None);
            await Payrolls.CalculateAsync(period.Id, CancellationToken.None);
            await Payrolls.SubmitAsync(period.Id, CancellationToken.None);
            User.SignInAs(UserRole.Admin);
            await Payrolls.ApproveAsync(period.Id, CancellationToken.None);
            return period.Id;
        }

        public async Task<Guid> FinalizedAsync()
        {
            var id = await ApprovedAsync();
            await Payrolls.FinalizeAsync(id, CancellationToken.None);
            return id;
        }

        public Task<PaymentBatchDto> CreateBatchAsync(Guid periodId, DateOnly? date = null) =>
            Service.CreateBatchAsync(new CreatePaymentBatchDto { PayrollPeriodId = periodId, PaymentMethod = PaymentMethod.BankTransfer, PaymentDate = date }, CancellationToken.None);

        public PaymentTransaction TransactionOf(Employee employee) => Payments.Transactions.Last(t => t.EmployeeId == employee.Id);

        public PayrollPeriod Period => Payroll.Periods.Single();
    }

    // ---- batch creation ----

    [Fact]
    public async Task CreateBatch_FromFinalizedPayroll_CalculatesTotalsAndItems()
    {
        var s = new Setup();
        var id = await s.FinalizedAsync();

        var batch = await s.CreateBatchAsync(id);

        Assert.Equal("PAY-202610-0001", batch.BatchNumber);
        Assert.Equal("Pending", batch.Status);
        Assert.Equal(2, batch.TotalEmployees);
        Assert.Equal(72500m, batch.TotalAmount); // 30500 + 42000, from the records, not the request
        Assert.Equal("BankTransfer", batch.PaymentMethod);
        Assert.Equal(new DateOnly(2026, 11, 5), batch.PaymentDate); // default: today's office date
        Assert.Equal(2, batch.Counts.Pending);
        Assert.Equal(30500m, s.TransactionOf(s.Alice).Amount);
        var history = Assert.Single(s.TransactionOf(s.Alice).History);
        Assert.Null(history.PreviousStatus);
        Assert.Equal(PaymentTransactionStatus.Pending, history.NewStatus);
        Assert.Contains(s.Audit.Entries, e => e.Action == "PaymentBatchCreated");
    }

    [Fact]
    public async Task CreateBatch_FromApprovedButNotFinalizedPayroll_IsRefused()
    {
        var s = new Setup();
        var id = await s.ApprovedAsync();

        var error = await Assert.ThrowsAsync<BadRequestException>(() => s.CreateBatchAsync(id));
        Assert.Equal("Payroll must be finalized before payment.", error.Message);
    }

    [Fact]
    public async Task CreateBatch_Twice_IsRefused()
    {
        var s = new Setup();
        var id = await s.FinalizedAsync();
        await s.CreateBatchAsync(id);

        var error = await Assert.ThrowsAsync<ConflictException>(() => s.CreateBatchAsync(id));
        Assert.Equal("Payment batch already exists for this payroll.", error.Message);
        Assert.Single(s.Payments.Batches);
    }

    [Fact]
    public async Task CreateBatch_PaymentDateBeforeThePeriod_IsRefused()
    {
        var s = new Setup();
        var id = await s.FinalizedAsync();

        await Assert.ThrowsAsync<BadRequestException>(() => s.CreateBatchAsync(id, new DateOnly(2026, 9, 30)));
    }

    [Fact]
    public async Task UnknownPayroll_IsNotFound()
    {
        var s = new Setup();
        s.User.SignInAs(UserRole.Admin);

        await Assert.ThrowsAsync<NotFoundException>(() => s.CreateBatchAsync(Guid.NewGuid()));
    }

    // ---- processing ----

    [Fact]
    public async Task FullLifecycle_Pending_Processing_Paid_UpdatesPayslipBatchAndPayroll()
    {
        var s = new Setup();
        var id = await s.FinalizedAsync();
        var batch = await s.CreateBatchAsync(id);

        var processing = await s.Service.ProcessBatchAsync(batch.Id, CancellationToken.None);
        Assert.Equal("Processing", processing.Status);
        Assert.Equal(2, processing.Counts.Processing);

        var paid = await s.Service.MarkPaidAsync(s.TransactionOf(s.Alice).Id, new MarkPaymentPaidDto { TransactionReference = "TRX-1001", PaymentDate = new DateOnly(2026, 11, 3) }, CancellationToken.None);
        Assert.Equal("Paid", paid.Status);
        Assert.Equal("TRX-1001", paid.TransactionReference);
        Assert.Equal(new DateOnly(2026, 11, 3), paid.PaymentDate);
        Assert.Equal(new[] { "Pending", "Processing", "Paid" }, paid.History!.Select(h => h.NewStatus));

        var payslip = s.Period.Records.Single(r => r.EmployeeId == s.Alice.Id).Payslip!;
        Assert.Equal(PaymentStatus.Paid, payslip.PaymentStatus);
        Assert.Equal(PaymentMethod.BankTransfer, payslip.PaymentMethod);
        Assert.Equal("TRX-1001", payslip.PaymentReference);
        Assert.Equal(PaymentBatchStatus.Processing, s.Payments.Batches.Single().Status); // Bob is still in progress
        Assert.Equal(PayrollPeriodStatus.Finalized, s.Period.Status);

        await s.Service.MarkPaidAsync(s.TransactionOf(s.Bob).Id, new MarkPaymentPaidDto(), CancellationToken.None);
        Assert.Equal(PaymentBatchStatus.Paid, s.Payments.Batches.Single().Status);
        Assert.Equal(PayrollPeriodStatus.Paid, s.Period.Status);
        Assert.All(s.Period.Records, r => Assert.Equal(PayrollRecordStatus.Paid, r.Status));
        Assert.Contains(s.Audit.Entries, e => e.Action == "PaymentMarkedPaid");
        Assert.Contains(s.Audit.Entries, e => e.Action == "PayrollPaid");
    }

    [Fact]
    public async Task Failed_ThenRetry_ThenPaid()
    {
        var s = new Setup();
        var id = await s.FinalizedAsync();
        var batch = await s.CreateBatchAsync(id);
        await s.Service.ProcessBatchAsync(batch.Id, CancellationToken.None);
        var alice = s.TransactionOf(s.Alice).Id;

        var failed = await s.Service.MarkFailedAsync(alice, new PaymentReasonDto { Reason = "Account closed" }, CancellationToken.None);
        Assert.Equal("Failed", failed.Status);
        Assert.Equal("Account closed", failed.FailureReason);

        var retried = await s.Service.RetryAsync(alice, new PaymentReasonDto(), CancellationToken.None);
        Assert.Equal("Processing", retried.Status);
        Assert.Null(retried.FailureReason);

        var paid = await s.Service.MarkPaidAsync(alice, new MarkPaymentPaidDto(), CancellationToken.None);
        Assert.Equal(new[] { "Pending", "Processing", "Failed", "Processing", "Paid" }, paid.History!.Select(h => h.NewStatus));
        Assert.Equal(5, s.Payments.HistoryAdds); // 2 processed, failed, retried, paid: every change registered for insert
        Assert.Contains(s.Audit.Entries, e => e.Action == "PaymentMarkedFailed");
        Assert.Contains(s.Audit.Entries, e => e.Action == "PaymentRetried");
    }

    [Fact]
    public async Task InvalidTransitions_AreRejected()
    {
        var s = new Setup();
        var id = await s.FinalizedAsync();
        await s.CreateBatchAsync(id);
        var alice = s.TransactionOf(s.Alice).Id;

        // Pending can't be paid, failed or retried directly.
        await Assert.ThrowsAsync<ConflictException>(() => s.Service.MarkPaidAsync(alice, new MarkPaymentPaidDto(), CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() => s.Service.MarkFailedAsync(alice, new PaymentReasonDto { Reason = "x" }, CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() => s.Service.RetryAsync(alice, new PaymentReasonDto(), CancellationToken.None));

        await s.Service.ProcessBatchAsync(s.Payments.Batches.Single().Id, CancellationToken.None);
        await s.Service.MarkPaidAsync(alice, new MarkPaymentPaidDto(), CancellationToken.None);

        var again = await Assert.ThrowsAsync<ConflictException>(() => s.Service.MarkPaidAsync(alice, new MarkPaymentPaidDto(), CancellationToken.None));
        Assert.Equal("Payment has already been completed.", again.Message);
        await Assert.ThrowsAsync<ConflictException>(() => s.Service.MarkFailedAsync(alice, new PaymentReasonDto { Reason = "x" }, CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() => s.Service.CancelAsync(alice, new PaymentReasonDto { Reason = "x" }, CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() => s.Service.RetryAsync(alice, new PaymentReasonDto(), CancellationToken.None));
    }

    [Fact]
    public async Task CancelledPayment_CannotBePaid_AndIsPaidInALaterBatch()
    {
        var s = new Setup();
        var id = await s.FinalizedAsync();
        var first = await s.CreateBatchAsync(id);
        var alice = s.TransactionOf(s.Alice).Id;

        await s.Service.CancelAsync(alice, new PaymentReasonDto { Reason = "Wrong account" }, CancellationToken.None);
        await Assert.ThrowsAsync<ConflictException>(() => s.Service.MarkPaidAsync(alice, new MarkPaymentPaidDto(), CancellationToken.None));

        await s.Service.ProcessBatchAsync(first.Id, CancellationToken.None);
        await s.Service.MarkPaidAsync(s.TransactionOf(s.Bob).Id, new MarkPaymentPaidDto(), CancellationToken.None);
        var firstNow = await s.Service.GetBatchAsync(first.Id, CancellationToken.None);
        Assert.Equal("Paid", firstNow.Status);         // the cancelled payment is ignored
        Assert.Equal(1, firstNow.TotalEmployees);
        Assert.Equal(PayrollPeriodStatus.Finalized, s.Period.Status); // Alice is still unpaid

        var second = await s.CreateBatchAsync(id);
        Assert.Equal("PAY-202610-0002", second.BatchNumber);
        Assert.Equal(1, second.TotalEmployees);
        Assert.Equal(30500m, second.TotalAmount);
        Assert.Equal(s.Alice.Id, s.Payments.Batches.Last().Items.Single().EmployeeId);
    }

    [Fact]
    public async Task NobodyIsPaidTwice_ForTheSamePayroll()
    {
        var s = new Setup();
        var id = await s.FinalizedAsync();
        var batch = await s.CreateBatchAsync(id);
        await s.Service.ProcessBatchAsync(batch.Id, CancellationToken.None);
        await s.Service.MarkPaidAsync(s.TransactionOf(s.Alice).Id, new MarkPaymentPaidDto(), CancellationToken.None);
        await s.Service.MarkPaidAsync(s.TransactionOf(s.Bob).Id, new MarkPaymentPaidDto(), CancellationToken.None);

        // Fully paid payroll: no further batch.
        await Assert.ThrowsAsync<BadRequestException>(() => s.CreateBatchAsync(id));
        Assert.Equal(2, s.Payments.Transactions.Count());
    }

    [Fact]
    public async Task ConcurrentDuplicateBatch_IsRejectedOnSave()
    {
        var s = new Setup();
        var id = await s.FinalizedAsync();
        await s.CreateBatchAsync(id);

        // Simulate a request that passed the "open batch" check before the first one was saved.
        var open = s.Payments.Batches.Single();
        open.Status = PaymentBatchStatus.Paid; // hide it from the pre-check only
        var duplicate = s.CreateBatchAsync(id);
        open.Status = PaymentBatchStatus.Pending;
        await Assert.ThrowsAsync<BadRequestException>(() => duplicate);
        Assert.Single(s.Payments.Batches);
    }

    [Fact]
    public async Task CancelBatch_OnlyWhileEveryPaymentIsPending()
    {
        var s = new Setup();
        var id = await s.FinalizedAsync();
        var batch = await s.CreateBatchAsync(id);

        await Assert.ThrowsAsync<BadRequestException>(() => s.Service.CancelBatchAsync(batch.Id, new PaymentReasonDto(), CancellationToken.None));
        var cancelled = await s.Service.CancelBatchAsync(batch.Id, new PaymentReasonDto { Reason = "Wrong method" }, CancellationToken.None);
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal(0, cancelled.TotalEmployees);

        var second = await s.CreateBatchAsync(id);
        await s.Service.ProcessBatchAsync(second.Id, CancellationToken.None);
        await Assert.ThrowsAsync<ConflictException>(() => s.Service.CancelBatchAsync(second.Id, new PaymentReasonDto { Reason = "x" }, CancellationToken.None));
    }

    [Fact]
    public async Task Reasons_AreRequired_ForFailureAndCancellation_AndPaymentDateIsValidated()
    {
        var s = new Setup();
        var id = await s.FinalizedAsync();
        var batch = await s.CreateBatchAsync(id);
        await Assert.ThrowsAsync<BadRequestException>(() => s.Service.CancelAsync(s.TransactionOf(s.Alice).Id, new PaymentReasonDto { Reason = "  " }, CancellationToken.None));

        await s.Service.ProcessBatchAsync(batch.Id, CancellationToken.None);
        await Assert.ThrowsAsync<BadRequestException>(() => s.Service.MarkFailedAsync(s.TransactionOf(s.Alice).Id, new PaymentReasonDto(), CancellationToken.None));
        await Assert.ThrowsAsync<BadRequestException>(() => s.Service.MarkPaidAsync(s.TransactionOf(s.Alice).Id, new MarkPaymentPaidDto { PaymentDate = new DateOnly(2026, 11, 6) }, CancellationToken.None));
        await Assert.ThrowsAsync<BadRequestException>(() => s.Service.MarkPaidAsync(s.TransactionOf(s.Alice).Id, new MarkPaymentPaidDto { PaymentDate = new DateOnly(2026, 9, 1) }, CancellationToken.None));
    }

    // ---- authorization ----

    [Theory]
    [InlineData(UserRole.HR)]
    [InlineData(UserRole.Manager)]
    [InlineData(UserRole.Employee)]
    public async Task OnlyAdmin_CreatesAndChangesPayments(UserRole role)
    {
        var s = new Setup();
        var id = await s.FinalizedAsync();
        var batch = await s.CreateBatchAsync(id);
        var alice = s.TransactionOf(s.Alice).Id;
        s.User.SignInAs(role, s.Bob.Id);

        await Assert.ThrowsAsync<ForbiddenException>(() => s.CreateBatchAsync(id));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.ProcessBatchAsync(batch.Id, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.CancelBatchAsync(batch.Id, new PaymentReasonDto { Reason = "x" }, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.MarkPaidAsync(alice, new MarkPaymentPaidDto(), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.MarkFailedAsync(alice, new PaymentReasonDto { Reason = "x" }, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.RetryAsync(alice, new PaymentReasonDto(), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.CancelAsync(alice, new PaymentReasonDto { Reason = "x" }, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Payrolls.FinalizeAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task HrCanView_EmployeesCannotListBatchesOrHistory()
    {
        var s = new Setup();
        var id = await s.FinalizedAsync();
        await s.CreateBatchAsync(id);

        s.User.SignInAs(UserRole.HR);
        Assert.Equal(1, (await s.Service.GetBatchesAsync(new PaymentBatchQueryDto(), CancellationToken.None)).TotalCount);
        Assert.Equal(2, (await s.Service.GetPaymentsAsync(new PaymentQueryDto(), CancellationToken.None)).TotalCount);

        s.User.SignInAs(UserRole.Employee, s.Alice.Id);
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.GetBatchesAsync(new PaymentBatchQueryDto(), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.GetPaymentsAsync(new PaymentQueryDto(), CancellationToken.None));
    }

    [Fact]
    public async Task Employee_SeesOwnPaymentOnly()
    {
        var s = new Setup();
        var id = await s.FinalizedAsync();
        await s.CreateBatchAsync(id);
        s.User.SignInAs(UserRole.Employee, s.Alice.Id);

        var mine = await s.Service.GetMyPaymentsAsync(new PaymentQueryDto { EmployeeId = s.Bob.Id }, CancellationToken.None);
        Assert.Equal(s.Alice.Id, Assert.Single(mine.Items).EmployeeId);
        Assert.False(mine.Items[0].Actions.CanMarkPaid);
        Assert.Equal("EMP-001", (await s.Service.GetPaymentAsync(s.TransactionOf(s.Alice).Id, CancellationToken.None)).EmployeeCode);
        await Assert.ThrowsAsync<NotFoundException>(() => s.Service.GetPaymentAsync(s.TransactionOf(s.Bob).Id, CancellationToken.None));
    }

    [Fact]
    public async Task Admin_CannotChangeOwnSalaryPayment()
    {
        var s = new Setup();
        var id = await s.FinalizedAsync();
        var batch = await s.CreateBatchAsync(id);
        await s.Service.ProcessBatchAsync(batch.Id, CancellationToken.None);
        s.User.SignInAs(UserRole.Admin, s.Alice.Id);

        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.MarkPaidAsync(s.TransactionOf(s.Alice).Id, new MarkPaymentPaidDto(), CancellationToken.None));
        Assert.Equal("Paid", (await s.Service.MarkPaidAsync(s.TransactionOf(s.Bob).Id, new MarkPaymentPaidDto(), CancellationToken.None)).Status);
    }

    // ---- batch status ----

    [Theory]
    [InlineData(new[] { PaymentTransactionStatus.Pending, PaymentTransactionStatus.Pending }, PaymentBatchStatus.Pending)]
    [InlineData(new[] { PaymentTransactionStatus.Processing, PaymentTransactionStatus.Paid }, PaymentBatchStatus.Processing)]
    [InlineData(new[] { PaymentTransactionStatus.Paid, PaymentTransactionStatus.Pending }, PaymentBatchStatus.PartiallyPaid)]
    [InlineData(new[] { PaymentTransactionStatus.Paid, PaymentTransactionStatus.Failed }, PaymentBatchStatus.PartiallyPaid)]
    [InlineData(new[] { PaymentTransactionStatus.Paid, PaymentTransactionStatus.Paid }, PaymentBatchStatus.Paid)]
    [InlineData(new[] { PaymentTransactionStatus.Paid, PaymentTransactionStatus.Cancelled }, PaymentBatchStatus.Paid)]
    [InlineData(new[] { PaymentTransactionStatus.Failed, PaymentTransactionStatus.Failed }, PaymentBatchStatus.Failed)]
    [InlineData(new[] { PaymentTransactionStatus.Cancelled, PaymentTransactionStatus.Cancelled }, PaymentBatchStatus.Cancelled)]
    public void BatchStatus_FollowsItsPayments(PaymentTransactionStatus[] payments, PaymentBatchStatus expected)
    {
        Assert.Equal(expected, PaymentRules.BatchStatus(payments));
    }

    [Theory]
    [InlineData(PaymentTransactionStatus.Pending, PaymentTransactionStatus.Processing, true)]
    [InlineData(PaymentTransactionStatus.Pending, PaymentTransactionStatus.Cancelled, true)]
    [InlineData(PaymentTransactionStatus.Processing, PaymentTransactionStatus.Paid, true)]
    [InlineData(PaymentTransactionStatus.Processing, PaymentTransactionStatus.Failed, true)]
    [InlineData(PaymentTransactionStatus.Failed, PaymentTransactionStatus.Processing, true)]
    [InlineData(PaymentTransactionStatus.Paid, PaymentTransactionStatus.Pending, false)]
    [InlineData(PaymentTransactionStatus.Paid, PaymentTransactionStatus.Processing, false)]
    [InlineData(PaymentTransactionStatus.Paid, PaymentTransactionStatus.Failed, false)]
    [InlineData(PaymentTransactionStatus.Cancelled, PaymentTransactionStatus.Paid, false)]
    [InlineData(PaymentTransactionStatus.Pending, PaymentTransactionStatus.Paid, false)]
    [InlineData(PaymentTransactionStatus.Failed, PaymentTransactionStatus.Cancelled, false)]
    public void Transitions(PaymentTransactionStatus from, PaymentTransactionStatus to, bool allowed)
    {
        Assert.Equal(allowed, PaymentRules.CanMove(from, to));
    }
}
