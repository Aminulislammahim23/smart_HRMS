namespace smartHRMS.Application.Common.Security;

/// <summary>Names of audited actions, as stored in AuditLogs.Action.</summary>
public static class AuditActions
{
    public const string LoginSucceeded = "LoginSucceeded";
    public const string LoginFailed = "LoginFailed";
    public const string PasswordChanged = "PasswordChanged";
    public const string PasswordReset = "PasswordReset";
    public const string UserCreated = "UserCreated";
    public const string UserUpdated = "UserUpdated";

    public const string ManagerAssigned = "ManagerAssigned";
    public const string SalaryStructureUpdated = "SalaryStructureUpdated";

    public const string LeaveRequested = "LeaveRequested";
    public const string LeaveApproved = "LeaveApproved";
    public const string LeaveRejected = "LeaveRejected";
    public const string LeaveCancelled = "LeaveCancelled";

    public const string PayrollPeriodCreated = "PayrollPeriodCreated";
    public const string PayrollPeriodUpdated = "PayrollPeriodUpdated";
    public const string PayrollPeriodDeleted = "PayrollPeriodDeleted";
    public const string PayrollCalculated = "PayrollCalculated";
    public const string PayrollRecordUpdated = "PayrollRecordUpdated";
    public const string PayrollSubmitted = "PayrollSubmitted";
    public const string PayrollApproved = "PayrollApproved";
    public const string PayrollPaid = "PayrollPaid";
    public const string PayrollCancelled = "PayrollCancelled";
    public const string PayslipViewed = "PayslipViewed";
    public const string PayslipsGenerated = "PayslipsGenerated";
    public const string PayslipPaid = "PayslipPaid";
    public const string PayrollFinalized = "PayrollFinalized";
    public const string PayrollLocked = "PayrollLocked";
    public const string PaymentBatchCreated = "PaymentBatchCreated";
    public const string PaymentBatchCancelled = "PaymentBatchCancelled";
    public const string PaymentProcessingStarted = "PaymentProcessingStarted";
    public const string PaymentMarkedPaid = "PaymentMarkedPaid";
    public const string PaymentMarkedFailed = "PaymentMarkedFailed";
    public const string PaymentRetried = "PaymentRetried";
    public const string PaymentCancelled = "PaymentCancelled";

    // Day 19: reporting and exports (no amounts in the details).
    public const string PayrollReportExported = "PayrollReportExported";
    public const string PayslipsExported = "PayslipsExported";
}
