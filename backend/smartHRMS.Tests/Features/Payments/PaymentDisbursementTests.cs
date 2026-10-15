using System.Text;
using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Features.Payments;
using smartHRMS.Application.Features.Payments.Dtos;
using smartHRMS.Application.Features.Payroll.Dtos;
using smartHRMS.Domain.Entities;
using smartHRMS.Domain.Enums;
using Xunit;
using Setup = smartHRMS.Tests.Features.Payments.PaymentServiceTests.Setup;

namespace smartHRMS.Tests.Features.Payments;

/// <summary>
/// Day 21 — payroll disbursement and payment tracking on top of the Day 18 payment batches: eligibility, payment
/// evidence, failure history and retries, Paid corrections, financial integrity, the disbursement dashboard, the
/// payment export and authorization.
/// </summary>
public class PaymentDisbursementTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    private static MarkPaymentPaidDto Evidence(string reference = "TRX-1001") => new() { TransactionReference = reference };

    private static Task<DisbursementSummaryDto> SummaryAsync(Setup s, Guid? periodId = null) =>
        s.Service.GetSummaryAsync(new DisbursementSummaryQueryDto { PayrollPeriodId = periodId }, None);

    /// <summary>Finalized payroll with a batch whose payments are all Processing.</summary>
    private static async Task<(Setup S, Guid PeriodId)> ProcessingAsync(PaymentMethod method = PaymentMethod.BankTransfer)
    {
        var s = new Setup();
        var id = await s.FinalizedAsync();
        var batch = await s.Service.CreateBatchAsync(new CreatePaymentBatchDto { PayrollPeriodId = id, PaymentMethod = method }, None);
        await s.Service.ProcessBatchAsync(batch.Id, None);
        return (s, id);
    }

    // ---- eligibility ----

    [Fact]
    public async Task DraftCalculatedPendingApprovedAndRejectedPayroll_AreNeverEligible()
    {
        var s = new Setup();
        s.User.SignInAs(UserRole.HR);
        var period = await s.Payrolls.CreatePeriodAsync(new CreatePayrollPeriodDto { StartDate = new DateOnly(2026, 10, 1), EndDate = new DateOnly(2026, 10, 31) }, None);
        s.User.SignInAs(UserRole.Admin);

        async Task AssertNotEligibleAsync()
        {
            Assert.Equal(0, (await SummaryAsync(s)).Payrolls.TotalCount);
            await Assert.ThrowsAsync<BadRequestException>(() => s.CreateBatchAsync(period.Id));
        }

        await AssertNotEligibleAsync(); // Draft
        s.User.SignInAs(UserRole.HR);
        await s.Payrolls.CalculateAsync(period.Id, None);
        s.User.SignInAs(UserRole.Admin);
        await AssertNotEligibleAsync(); // Calculated (processing)
        s.User.SignInAs(UserRole.HR);
        await s.Payrolls.SubmitAsync(period.Id, None);
        s.User.SignInAs(UserRole.Admin);
        await AssertNotEligibleAsync(); // PendingApproval
        await s.Payrolls.CancelAsync(period.Id, new CancelPayrollDto { Reason = "Rejected in review" }, None);
        Assert.Equal(PayrollPeriodStatus.Cancelled, s.Period.Status);
        await AssertNotEligibleAsync(); // rejected (Cancelled)
        Assert.Empty(s.Payments.Batches);
    }

    [Fact]
    public async Task ApprovedButNotFinalized_IsNotEligible_FinalizedIs()
    {
        var s = new Setup();
        var id = await s.ApprovedAsync();
        Assert.Equal(0, (await SummaryAsync(s)).Payrolls.TotalCount);

        await s.Payrolls.FinalizeAsync(id, None);
        var row = Assert.Single((await SummaryAsync(s)).Payrolls.Items);
        Assert.Equal(id, row.PayrollPeriodId);
        Assert.Equal("Finalized", row.PayrollStatus);
        Assert.Equal(2, row.PayableCount);
        Assert.Equal(72500m, row.PayableAmount); // 30500 + 42000, the locked net salaries
        Assert.Equal(2, row.NotInitiatedCount);
        Assert.Equal(72500m, row.NotInitiatedAmount);
        Assert.True(row.CanCreateBatch);
        Assert.False(row.HasOpenBatch);
    }

    [Fact]
    public async Task UnknownPayrollReference_IsRejected_AndSummaryOfUnknownPayrollIsEmpty()
    {
        var s = new Setup();
        await s.FinalizedAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => s.CreateBatchAsync(Guid.NewGuid()));
        Assert.Equal(0, (await SummaryAsync(s, Guid.NewGuid())).Payrolls.TotalCount);
    }

    // ---- payment evidence ----

    [Theory]
    [InlineData(PaymentMethod.BankTransfer)]
    [InlineData(PaymentMethod.MobileBanking)]
    [InlineData(PaymentMethod.Cheque)]
    public async Task MarkPaid_WithoutReference_IsRejected_ForTraceableMethods(PaymentMethod method)
    {
        var (s, _) = await ProcessingAsync(method);
        var alice = s.TransactionOf(s.Alice);
        var historyBefore = alice.History.Count;

        var error = await Assert.ThrowsAsync<BadRequestException>(() =>
            s.Service.MarkPaidAsync(alice.Id, new MarkPaymentPaidDto { Remarks = "Sent, reference to follow" }, None));
        Assert.Contains("transaction reference is required", error.Message);
        Assert.Equal(PaymentTransactionStatus.Processing, alice.Status);
        Assert.Equal(historyBefore, alice.History.Count);
        Assert.Equal(PaymentStatus.Unpaid, s.Period.Records.Single(r => r.EmployeeId == s.Alice.Id).Payslip!.PaymentStatus);
    }

    [Fact]
    public async Task MarkPaid_Cash_NeedsAReferenceOrRemarks()
    {
        var (s, _) = await ProcessingAsync(PaymentMethod.Cash);
        var alice = s.TransactionOf(s.Alice).Id;

        await Assert.ThrowsAsync<BadRequestException>(() => s.Service.MarkPaidAsync(alice, new MarkPaymentPaidDto { Remarks = "   " }, None));

        var paid = await s.Service.MarkPaidAsync(alice, new MarkPaymentPaidDto { Remarks = "Handed over in office, signed receipt" }, None);
        Assert.Equal("Paid", paid.Status);
        Assert.Null(paid.TransactionReference);
        Assert.Equal("Payment confirmed: Handed over in office, signed receipt", paid.History!.Last().Reason);

        var bob = await s.Service.MarkPaidAsync(s.TransactionOf(s.Bob).Id, new MarkPaymentPaidDto { TransactionReference = "RCPT-0042" }, None);
        Assert.Equal("RCPT-0042", bob.History!.Last().Reference);
    }

    // ---- failure history and retry ----

    [Fact]
    public async Task FailedAttempt_KeepsReasonAndReference_ThroughRetryWithAnotherMethod()
    {
        var (s, _) = await ProcessingAsync();
        var alice = s.TransactionOf(s.Alice).Id;

        await s.Service.MarkFailedAsync(alice, new MarkPaymentFailedDto { Reason = "Account closed", TransactionReference = "BANK-REJ-77" }, None);
        var retried = await s.Service.RetryAsync(alice, new RetryPaymentDto { Reason = "Pay in cash", PaymentMethod = PaymentMethod.Cash }, None);
        Assert.Equal("Processing", retried.Status);
        Assert.Equal("Cash", retried.PaymentMethod);
        Assert.Null(retried.FailureReason); // the current reason is cleared ...

        var paid = await s.Service.MarkPaidAsync(alice, new MarkPaymentPaidDto { TransactionReference = "RCPT-7", Remarks = "Cash, signed receipt" }, None);

        // ... but the failed attempt, its reason and its reference stay in the history.
        var failed = Assert.Single(paid.History!, h => h.NewStatus == "Failed");
        Assert.Equal("Account closed", failed.Reason);
        Assert.Equal("BANK-REJ-77", failed.Reference);
        Assert.Contains(paid.History!, h => h.NewStatus == "Processing" && h.Reason!.Contains("Method BankTransfer → Cash"));
        Assert.Equal(2, paid.AttemptCount);
        Assert.Equal(1, paid.FailedAttemptCount);
        Assert.Equal("RCPT-7", paid.TransactionReference);
        Assert.Equal(PaymentMethod.Cash, s.Period.Records.Single(r => r.EmployeeId == s.Alice.Id).Payslip!.PaymentMethod);
        Assert.Contains(s.Audit.Entries, e => e.Action == "PaymentMarkedFailed" && e.Details!.Contains("BANK-REJ-77"));
        Assert.Contains(s.Audit.Entries, e => e.Action == "PaymentRetried" && e.Details!.Contains("BankTransfer → Cash"));
    }

    [Fact]
    public async Task Retry_IsOnlyForFailedPayments_AndAFailureNeedsAReason()
    {
        var (s, _) = await ProcessingAsync();
        var alice = s.TransactionOf(s.Alice).Id;

        await Assert.ThrowsAsync<ConflictException>(() => s.Service.RetryAsync(alice, new RetryPaymentDto(), None)); // Processing
        await Assert.ThrowsAsync<BadRequestException>(() => s.Service.MarkFailedAsync(alice, new MarkPaymentFailedDto { TransactionReference = "X-1" }, None));
        Assert.Equal(PaymentTransactionStatus.Processing, s.TransactionOf(s.Alice).Status);
    }

    // ---- corrections ----

    [Fact]
    public async Task Correct_PaidPayment_KeepsItPaid_AndKeepsTheOldValuesInHistory()
    {
        var (s, _) = await ProcessingAsync();
        var alice = s.TransactionOf(s.Alice).Id;
        await s.Service.MarkPaidAsync(alice, new MarkPaymentPaidDto { TransactionReference = "TRX-1O01", PaymentDate = new DateOnly(2026, 11, 3) }, None);

        var corrected = await s.Service.CorrectAsync(alice, new CorrectPaymentDto { TransactionReference = "TRX-1001", PaymentDate = new DateOnly(2026, 11, 4), Reason = "Typo in the bank reference" }, None);

        Assert.Equal("Paid", corrected.Status);
        Assert.Equal(30500m, corrected.Amount);
        Assert.Equal("TRX-1001", corrected.TransactionReference);
        Assert.Equal(new DateOnly(2026, 11, 4), corrected.PaymentDate);
        var row = corrected.History!.Last();
        Assert.Equal(("Paid", "Paid"), (row.PreviousStatus, row.NewStatus));
        Assert.Equal("TRX-1001", row.Reference);
        Assert.Contains("reference was 'TRX-1O01'", row.Reason);
        Assert.Contains("date was 2026-11-03", row.Reason);
        Assert.Contains(corrected.History!, h => h.NewStatus == "Paid" && h.PreviousStatus == "Processing" && h.Reference == "TRX-1O01");

        var payslip = s.Period.Records.Single(r => r.EmployeeId == s.Alice.Id).Payslip!;
        Assert.Equal(("TRX-1001", new DateOnly(2026, 11, 4)), (payslip.PaymentReference, payslip.PaymentDate));
        Assert.Equal(PaymentStatus.Paid, payslip.PaymentStatus);
        var audit = Assert.Single(s.Audit.Entries, e => e.Action == "PaymentCorrected");
        Assert.DoesNotContain("30500", audit.Details);
    }

    [Fact]
    public async Task Correct_IsRefused_WithoutReason_WithoutChange_OrUnlessPaid()
    {
        var (s, _) = await ProcessingAsync();
        var alice = s.TransactionOf(s.Alice).Id;

        await Assert.ThrowsAsync<ConflictException>(() => s.Service.CorrectAsync(alice, new CorrectPaymentDto { TransactionReference = "X", Reason = "r" }, None)); // Processing
        await s.Service.MarkPaidAsync(alice, Evidence(), None);
        await Assert.ThrowsAsync<BadRequestException>(() => s.Service.CorrectAsync(alice, new CorrectPaymentDto { TransactionReference = "TRX-2" }, None));
        await Assert.ThrowsAsync<BadRequestException>(() => s.Service.CorrectAsync(alice, new CorrectPaymentDto { TransactionReference = "TRX-1001", Reason = "same" }, None));
        await Assert.ThrowsAsync<BadRequestException>(() => s.Service.CorrectAsync(alice, new CorrectPaymentDto { TransactionReference = "  ", Reason = "clear it" }, None)); // can't remove evidence
        await Assert.ThrowsAsync<BadRequestException>(() => s.Service.CorrectAsync(alice, new CorrectPaymentDto { PaymentDate = new DateOnly(2026, 12, 1), Reason = "future" }, None));
        Assert.Equal("TRX-1001", s.TransactionOf(s.Alice).TransactionReference);
    }

    [Fact]
    public async Task PaidPayment_CannotBeUnpaid_FailedCancelledOrRetried()
    {
        var (s, _) = await ProcessingAsync();
        var alice = s.TransactionOf(s.Alice).Id;
        await s.Service.MarkPaidAsync(alice, Evidence(), None);

        await Assert.ThrowsAsync<ConflictException>(() => s.Service.MarkFailedAsync(alice, new MarkPaymentFailedDto { Reason = "x" }, None));
        await Assert.ThrowsAsync<ConflictException>(() => s.Service.CancelAsync(alice, new PaymentReasonDto { Reason = "x" }, None));
        await Assert.ThrowsAsync<ConflictException>(() => s.Service.RetryAsync(alice, new RetryPaymentDto(), None));
        await Assert.ThrowsAsync<ConflictException>(() => s.Service.MarkPaidAsync(alice, Evidence("TRX-AGAIN"), None));
        Assert.Equal(PaymentTransactionStatus.Paid, s.TransactionOf(s.Alice).Status);
        Assert.Equal("TRX-1001", s.TransactionOf(s.Alice).TransactionReference);
    }

    // ---- financial integrity ----

    [Fact]
    public async Task PaymentTracking_NeverChangesTheFinalizedSalary()
    {
        var (s, _) = await ProcessingAsync();
        var before = s.Period.Records.Select(r => (r.EmployeeId, r.GrossSalary, r.TotalDeduction, r.NetSalary, r.Tax)).OrderBy(r => r.EmployeeId).ToList();

        var alice = s.TransactionOf(s.Alice).Id;
        await s.Service.MarkFailedAsync(alice, new MarkPaymentFailedDto { Reason = "Bounced" }, None);
        await s.Service.RetryAsync(alice, new RetryPaymentDto(), None);
        await s.Service.MarkPaidAsync(alice, Evidence(), None);
        await s.Service.CorrectAsync(alice, new CorrectPaymentDto { TransactionReference = "TRX-9", Reason = "Bank re-issued the reference" }, None);
        await s.Service.MarkPaidAsync(s.TransactionOf(s.Bob).Id, Evidence("TRX-1002"), None);

        var after = s.Period.Records.Select(r => (r.EmployeeId, r.GrossSalary, r.TotalDeduction, r.NetSalary, r.Tax)).OrderBy(r => r.EmployeeId).ToList();
        Assert.Equal(before, after);
        Assert.True(s.Period.IsLocked);
    }

    [Fact]
    public async Task MarkPaid_IsRefused_WhenTheAmountNoLongerMatchesTheLockedRecord()
    {
        var (s, _) = await ProcessingAsync();
        var record = s.Period.Records.Single(r => r.EmployeeId == s.Alice.Id);
        record.NetSalary = 99999m; // simulates data changed behind the application's back

        var error = await Assert.ThrowsAsync<ConflictException>(() => s.Service.MarkPaidAsync(s.TransactionOf(s.Alice).Id, Evidence(), None));
        Assert.Contains("no longer matches", error.Message);
        Assert.Equal(PaymentStatus.Unpaid, record.Payslip!.PaymentStatus);
        Assert.Equal(PaymentTransactionStatus.Processing, s.TransactionOf(s.Alice).Status);
    }

    [Fact]
    public async Task Summary_FollowsThePaymentWorkflow_AndPaidNeverExceedsPayable()
    {
        var s = new Setup();
        var id = await s.FinalizedAsync();
        var batch = await s.CreateBatchAsync(id);

        var pending = (await SummaryAsync(s)).Totals;
        Assert.Equal((2, 72500m, 0), (pending.PendingCount, pending.PendingAmount, pending.NotInitiatedCount));
        Assert.False((await SummaryAsync(s)).Payrolls.Items.Single().CanCreateBatch); // an open batch exists

        await s.Service.ProcessBatchAsync(batch.Id, None);
        await s.Service.MarkPaidAsync(s.TransactionOf(s.Alice).Id, Evidence(), None);
        await s.Service.MarkFailedAsync(s.TransactionOf(s.Bob).Id, new MarkPaymentFailedDto { Reason = "Wrong account" }, None);

        var mixed = (await SummaryAsync(s)).Totals;
        Assert.Equal(72500m, mixed.PayableAmount);
        Assert.Equal((1, 30500m), (mixed.PaidCount, mixed.PaidAmount));
        Assert.Equal(42000m, mixed.OutstandingAmount);
        Assert.Equal((1, 42000m), (mixed.FailedCount, mixed.FailedAmount));
        Assert.Equal((0, 0, 0m), (mixed.ProcessingCount, mixed.NotInitiatedCount, mixed.NotInitiatedAmount));

        await s.Service.RetryAsync(s.TransactionOf(s.Bob).Id, new RetryPaymentDto(), None);
        Assert.Equal(1, (await SummaryAsync(s)).Totals.ProcessingCount);
        await s.Service.MarkPaidAsync(s.TransactionOf(s.Bob).Id, Evidence("TRX-1002"), None);

        var done = await SummaryAsync(s);
        Assert.Equal(done.Totals.PayableAmount, done.Totals.PaidAmount);
        Assert.Equal(0m, done.Totals.OutstandingAmount);
        Assert.Equal("Paid", done.Payrolls.Items.Single().PayrollStatus);
        Assert.False(done.Payrolls.Items.Single().CanCreateBatch);
        Assert.Equal(done.Totals.PaidAmount, s.Payments.Transactions.Where(t => t.Status == PaymentTransactionStatus.Paid).Sum(t => t.Amount));
        await Assert.ThrowsAsync<BadRequestException>(() => s.CreateBatchAsync(id)); // nothing more can be paid
    }

    [Fact]
    public async Task Summary_CancelledPayment_IsNotInitiatedAgain()
    {
        var s = new Setup();
        var id = await s.FinalizedAsync();
        await s.CreateBatchAsync(id);
        await s.Service.CancelAsync(s.TransactionOf(s.Alice).Id, new PaymentReasonDto { Reason = "Wrong account" }, None);

        var totals = (await SummaryAsync(s)).Totals;
        Assert.Equal((1, 30500m), (totals.NotInitiatedCount, totals.NotInitiatedAmount));
        Assert.Equal((1, 42000m), (totals.PendingCount, totals.PendingAmount));
    }

    [Fact]
    public async Task DuplicateRequests_NeverCreateASecondPayment()
    {
        var s = new Setup();
        var id = await s.FinalizedAsync();
        await s.CreateBatchAsync(id);

        await Assert.ThrowsAsync<ConflictException>(() => s.CreateBatchAsync(id));
        Assert.Single(s.Payments.Batches);
        Assert.Equal(2, s.Payments.Transactions.Count());
        Assert.All(s.Payments.Transactions.GroupBy(t => t.EmployeeId), g => Assert.Single(g));
    }

    // ---- list filters and export ----

    [Fact]
    public async Task Payments_FilterByDepartment_AndCarryDepartmentAndPayslipNumber()
    {
        var s = new Setup();
        var id = await s.FinalizedAsync();
        await s.CreateBatchAsync(id);
        foreach (var record in s.Period.Records)
        {
            var employee = record.EmployeeId == s.Alice.Id ? s.Alice : s.Bob;
            employee.DepartmentId = employee.Department!.Id;
            record.Employee = employee;
        }

        var page = await s.Service.GetPaymentsAsync(new PaymentQueryDto { DepartmentId = s.Alice.DepartmentId }, None);
        var only = Assert.Single(page.Items);
        Assert.Equal(s.Alice.Id, only.EmployeeId);
        Assert.Equal("Ops", only.DepartmentName);
        Assert.Equal("PS-20261001-EMP-001", only.PayslipNumber);

        // An employee's own list ignores a department (or employee) filter.
        s.User.SignInAs(UserRole.Employee, s.Bob.Id);
        var mine = await s.Service.GetMyPaymentsAsync(new PaymentQueryDto { DepartmentId = s.Alice.DepartmentId }, None);
        Assert.Equal(s.Bob.Id, Assert.Single(mine.Items).EmployeeId);
    }

    [Fact]
    public async Task Export_WritesTheFilteredPayments_AndIsAuditedWithoutAmounts()
    {
        var (s, _) = await ProcessingAsync();
        await s.Service.MarkPaidAsync(s.TransactionOf(s.Alice).Id, Evidence(), None);
        s.User.SignInAs(UserRole.HR);

        var file = await s.Service.ExportAsync(new PaymentQueryDto { Status = "Paid" }, "csv", None);

        Assert.Equal("text/csv", file.ContentType);
        var lines = Encoding.UTF8.GetString(file.Content).Trim('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length); // header + Alice
        Assert.StartsWith("Employee ID,Employee,Department", lines[0]);
        Assert.Contains("EMP-001", lines[1]);
        Assert.Contains("TRX-1001", lines[1]);
        var audit = Assert.Single(s.Audit.Entries, e => e.Action == "PaymentsExported");
        Assert.Equal("1 payments exported as csv.", audit.Details);

        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", (await s.Service.ExportAsync(new PaymentQueryDto(), "xlsx", None)).ContentType);
        await Assert.ThrowsAsync<BadRequestException>(() => s.Service.ExportAsync(new PaymentQueryDto(), "pdf", None));
    }

    // ---- authorization ----

    [Theory]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.Manager)]
    public async Task EmployeesAndManagers_CannotSeeTheDashboardOrExport(UserRole role)
    {
        var s = new Setup();
        await s.FinalizedAsync();
        s.User.SignInAs(role, s.Alice.Id);

        await Assert.ThrowsAsync<ForbiddenException>(() => SummaryAsync(s));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.ExportAsync(new PaymentQueryDto(), "csv", None));
    }

    [Theory]
    [InlineData(UserRole.HR)]
    [InlineData(UserRole.Manager)]
    [InlineData(UserRole.Employee)]
    public async Task OnlyAdmin_RecordsOutcomesAndCorrections(UserRole role)
    {
        var (s, _) = await ProcessingAsync();
        var bob = s.TransactionOf(s.Bob).Id;
        await s.Service.MarkPaidAsync(s.TransactionOf(s.Alice).Id, Evidence(), None);
        var alice = s.TransactionOf(s.Alice).Id;
        s.User.SignInAs(role, s.Bob.Id); // even for their own salary

        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.MarkPaidAsync(bob, Evidence("TRX-SELF"), None));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.MarkFailedAsync(bob, new MarkPaymentFailedDto { Reason = "x" }, None));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.RetryAsync(bob, new RetryPaymentDto(), None));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.CorrectAsync(alice, new CorrectPaymentDto { TransactionReference = "Y", Reason = "x" }, None));
        Assert.Equal(PaymentTransactionStatus.Processing, s.TransactionOf(s.Bob).Status);

        // HR keeps read access; the summary never offers HR the create action.
        if (role == UserRole.HR)
        {
            Assert.False((await SummaryAsync(s)).Payrolls.Items.Single().CanCreateBatch);
            Assert.False((await s.Service.GetPaymentAsync(alice, None)).Actions.CanCorrect);
        }
    }

    [Fact]
    public async Task Admin_CannotConfirmOrCorrectTheirOwnSalaryPayment()
    {
        var (s, _) = await ProcessingAsync();
        await s.Service.MarkPaidAsync(s.TransactionOf(s.Bob).Id, Evidence("TRX-1002"), None);
        s.User.SignInAs(UserRole.Admin, s.Bob.Id);

        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.CorrectAsync(s.TransactionOf(s.Bob).Id, new CorrectPaymentDto { TransactionReference = "TRX-X", Reason = "x" }, None));
        Assert.False((await s.Service.GetPaymentAsync(s.TransactionOf(s.Bob).Id, None)).Actions.CanCorrect);
        Assert.True((await s.Service.MarkPaidAsync(s.TransactionOf(s.Alice).Id, Evidence(), None)).Actions.CanCorrect);
    }

    [Fact]
    public async Task Employee_SeesOwnPaymentHistoryWithAttempts_ButNoActions()
    {
        var (s, _) = await ProcessingAsync();
        var alice = s.TransactionOf(s.Alice).Id;
        await s.Service.MarkFailedAsync(alice, new MarkPaymentFailedDto { Reason = "Account closed" }, None);
        s.User.SignInAs(UserRole.Employee, s.Alice.Id);

        var own = await s.Service.GetPaymentAsync(alice, None);
        Assert.Equal("Failed", own.Status);
        Assert.Equal(1, own.FailedAttemptCount);
        Assert.False(own.Actions.CanRetry || own.Actions.CanMarkPaid || own.Actions.CanCorrect || own.Actions.CanMarkFailed || own.Actions.CanCancel);
        await Assert.ThrowsAsync<NotFoundException>(() => s.Service.GetPaymentAsync(s.TransactionOf(s.Bob).Id, None));
    }

    [Fact]
    public void EvidenceRule_IsDefinedPerMethod()
    {
        Assert.True(PaymentRules.RequiresReference(PaymentMethod.BankTransfer));
        Assert.True(PaymentRules.RequiresReference(PaymentMethod.MobileBanking));
        Assert.True(PaymentRules.RequiresReference(PaymentMethod.Cheque));
        Assert.False(PaymentRules.RequiresReference(PaymentMethod.Cash));
        Assert.False(PaymentRules.RequiresReference(PaymentMethod.Other));
        PaymentRules.EnsureEvidence(PaymentMethod.Other, null, "Paid by payroll card");
        Assert.Throws<BadRequestException>(() => PaymentRules.EnsureEvidence(PaymentMethod.Other, null, null));
    }
}
