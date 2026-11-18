using NickFinancas.Domain.Accounts;
using NickFinancas.Domain.Transactions;
using NickFinancas.Domain.Spaces;

namespace NickFinancas.Application.Finance;

public sealed record AccountItem(
    Guid Id,
    string Name,
    FinancialAccountType Type,
    decimal InitialBalance,
    decimal CurrentBalance);

public sealed record CategoryItem(
    Guid Id, string Name, TransactionType Type, Guid? ParentCategoryId = null, string? ParentName = null);

public sealed record TransactionItem(
    Guid Id,
    Guid AccountId,
    Guid CategoryId,
    TransactionType Type,
    string Description,
    decimal Amount,
    DateOnly Date,
    string AuthorName,
    Guid? DestinationAccountId,
    Guid? CreditCardId,
    int? InvoiceYear,
    int? InvoiceMonth,
    string DeviceName);

public sealed record TransactionExportResult(string Path, int TransactionCount);

public sealed record BankImportPreviewItem(
    Guid ImportId, DateOnly Date, decimal SignedAmount, string Description,
    bool AlreadyImported, bool PossibleMatch, bool RequiresReview, string Note);

public sealed record BankImportTransactionRequest(
    Guid ImportId, Guid AccountId, Guid CategoryId, TransactionType Type,
    string Description, decimal Amount, DateOnly Date, Guid AuthorId, string AuthorName);

public sealed record CreateTransactionRequest(
    Guid AccountId,
    Guid CategoryId,
    TransactionType Type,
    string Description,
    decimal Amount,
    DateOnly Date,
    Guid AuthorId,
    string AuthorName,
    Guid? DestinationAccountId = null,
    Guid? CreditCardId = null,
    int InstallmentCount = 1,
    int InstallmentStart = 1);

public sealed record UpdateTransactionRequest(
    Guid Id,
    Guid AccountId,
    Guid CategoryId,
    TransactionType Type,
    string Description,
    decimal Amount,
    DateOnly Date,
    Guid? DestinationAccountId = null,
    Guid? CreditCardId = null);

public sealed record CreateAccountRequest(
    string Name,
    FinancialAccountType Type,
    decimal InitialBalance);

public sealed record CreateCategoryRequest(
    string Name,
    TransactionType Type,
    Guid? ParentCategoryId = null);

public sealed record CreditCardItem(
    Guid Id,
    string Name,
    Guid PaymentAccountId,
    decimal CreditLimit,
    decimal SpendingLimit,
    int ClosingDay,
    int DueDay);

public sealed record CreateCreditCardRequest(
    string Name,
    Guid PaymentAccountId,
    decimal CreditLimit,
    int ClosingDay,
    int DueDay);

public sealed record UpdateCreditCardLimitRequest(
    Guid CreditCardId,
    decimal CreditLimit);

public sealed record UpdateCreditCardRequest(
    Guid CreditCardId,
    string Name,
    Guid PaymentAccountId,
    decimal CreditLimit,
    int ClosingDay,
    int DueDay);

public sealed record CreditCardInvoiceItem(
    Guid CreditCardId,
    string CardName,
    decimal CreditLimit,
    decimal MaximumLimit,
    decimal UsedLimit,
    decimal AvailableLimit,
    int Year,
    int Month,
    DateOnly ClosingDate,
    DateOnly DueDate,
    decimal Total,
    decimal Paid,
    decimal Outstanding,
    string Status);

public sealed record PayCreditCardInvoiceRequest(
    Guid CreditCardId,
    int InvoiceYear,
    int InvoiceMonth,
    DateOnly PaymentDate,
    Guid AuthorId,
    string AuthorName);

public sealed record PayableItem(
    Guid Id,
    string Description,
    decimal Amount,
    DateOnly DueDate,
    Guid AccountId,
    Guid CategoryId,
    string Status,
    bool IsRecurring,
    DateOnly? PaidDate);

public sealed record CreatePayableRequest(
    string Description,
    decimal Amount,
    DateOnly DueDate,
    Guid AccountId,
    Guid CategoryId,
    int InstallmentCount = 1,
    int InstallmentStart = 1);

public sealed record InstallmentPlanItem(
    Guid GroupId, string Description, string PaymentMethod,
    int TotalCount, int RemainingCount, DateOnly NextDueDate, decimal NextAmount,
    bool IsAccountPayable);

public sealed record CreateRecurringPayableRequest(
    string Description,
    decimal Amount,
    int DueDay,
    DateOnly StartDate,
    DateOnly? EndDate,
    Guid AccountId,
    Guid CategoryId);

public sealed record PayPayableRequest(
    Guid PayableId,
    DateOnly PaymentDate,
    Guid AuthorId,
    string AuthorName);

public sealed record RecurringPayableItem(
    Guid Id,
    string Description,
    decimal Amount,
    int DueDay,
    DateOnly StartDate,
    DateOnly? EndDate,
    Guid AccountId,
    Guid CategoryId);

public sealed record ReserveItem(Guid Id, string Name, decimal Balance);

public sealed record ReserveMovementItem(
    Guid Id, Guid ReserveId, Guid AccountId, decimal Amount, bool IsDeposit, DateOnly Date);

public sealed record CreateReserveRequest(string Name);
public sealed record UpdateReserveRequest(Guid Id, string Name);

public sealed record AddReserveMovementRequest(
    Guid ReserveId, Guid AccountId, decimal Amount, bool IsDeposit, DateOnly Date);

public sealed record TransferReserveRequest(
    Guid SourceReserveId, Guid DestinationReserveId, Guid AccountId, decimal Amount, DateOnly Date);

public sealed record GoalItem(
    Guid Id, string Name, decimal TargetAmount, DateOnly? TargetDate,
    decimal PlannedMonthlyContribution, Guid? ReserveId, decimal CurrentAmount,
    decimal AverageMonthlyContribution, bool IsPrimary);

public sealed record CreateGoalRequest(
    string Name, decimal TargetAmount, DateOnly? TargetDate,
    decimal PlannedMonthlyContribution, Guid? ReserveId);

public sealed record UpdateGoalRequest(
    Guid Id, string Name, decimal TargetAmount, DateOnly? TargetDate,
    decimal PlannedMonthlyContribution, Guid? ReserveId);

public sealed record FinancialSpaceItem(Guid Id, string Name, bool IsPrimary);
public sealed record LoginUserItem(string Name, bool HasPin);

public sealed record MemberItem(Guid Id, Guid SpaceId, string Name, FinancialSpaceRole Role, bool IsPrimary);

public sealed record SalarySettingsItem(Guid MemberId, decimal Amount, int PayDay, Guid AccountId);
public sealed record SaveSalarySettingsRequest(Guid MemberId, decimal Amount, int PayDay, Guid AccountId);

public sealed record AddMemberRequest(Guid SpaceId, string Name, FinancialSpaceRole Role);

public sealed record UpdateMemberRoleRequest(Guid Id, FinancialSpaceRole Role);

public sealed record BackupItem(string Path, DateTime CreatedAtUtc, long SizeBytes);
public sealed record BackupMaintenanceResult(bool Created, int AvailableBackups);

public sealed record DashboardSnapshot(
    decimal AvailableBalance,
    decimal MonthlyIncome,
    decimal MonthlyExpense,
    decimal TotalReserved,
    IReadOnlyList<AccountItem> Accounts,
    IReadOnlyList<CategoryItem> Categories,
    IReadOnlyList<TransactionItem> RecentTransactions);
