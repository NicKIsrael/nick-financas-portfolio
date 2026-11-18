namespace NickFinancas.Application.Finance;

public interface ILocalFinanceStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<DashboardSnapshot> GetDashboardAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task AddTransactionAsync(
        CreateTransactionRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TransactionItem>> GetTransactionsAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task<TransactionExportResult> ExportTransactionsCsvAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BankImportPreviewItem>> PreviewNubankCsvAsync(
        string path, Guid accountId, CancellationToken cancellationToken = default);

    Task<int> ImportBankTransactionsAsync(
        IReadOnlyList<BankImportTransactionRequest> transactions,
        CancellationToken cancellationToken = default);

    Task UpdateTransactionAsync(
        UpdateTransactionRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteTransactionAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default);

    Task AddAccountAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken = default);

    Task AddCategoryAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task DeleteCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CreditCardItem>> GetCreditCardsAsync(
        CancellationToken cancellationToken = default);

    Task AddCreditCardAsync(
        CreateCreditCardRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateCreditCardLimitAsync(
        UpdateCreditCardLimitRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateCreditCardAsync(
        UpdateCreditCardRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteCreditCardAsync(
        Guid creditCardId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CreditCardInvoiceItem>> GetCreditCardInvoicesAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task PayCreditCardInvoiceAsync(
        PayCreditCardInvoiceRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PayableItem>> GetPayablesAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task AddPayableAsync(
        CreatePayableRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InstallmentPlanItem>> GetInstallmentPlansAsync(
        CancellationToken cancellationToken = default);

    Task DeleteRemainingInstallmentsAsync(
        Guid groupId,
        CancellationToken cancellationToken = default);

    Task AddRecurringPayableAsync(
        CreateRecurringPayableRequest request,
        CancellationToken cancellationToken = default);

    Task PayPayableAsync(
        PayPayableRequest request,
        CancellationToken cancellationToken = default);

    Task DeletePayableAsync(
        Guid payableId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecurringPayableItem>> GetRecurringPayablesAsync(
        CancellationToken cancellationToken = default);

    Task DeleteRecurringPayableAsync(
        Guid recurringPayableId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReserveItem>> GetReservesAsync(CancellationToken cancellationToken = default);

    Task AddReserveAsync(CreateReserveRequest request, CancellationToken cancellationToken = default);
    Task UpdateReserveAsync(UpdateReserveRequest request, CancellationToken cancellationToken = default);
    Task DeleteReserveAsync(Guid reserveId, CancellationToken cancellationToken = default);

    Task AddReserveMovementAsync(
        AddReserveMovementRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReserveMovementItem>> GetReserveMovementsAsync(
        CancellationToken cancellationToken = default);

    Task TransferReserveAsync(TransferReserveRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GoalItem>> GetGoalsAsync(CancellationToken cancellationToken = default);

    Task AddGoalAsync(CreateGoalRequest request, CancellationToken cancellationToken = default);
    Task UpdateGoalAsync(UpdateGoalRequest request, CancellationToken cancellationToken = default);
    Task SetPrimaryGoalAsync(Guid goalId, CancellationToken cancellationToken = default);
    Task DeleteGoalAsync(Guid goalId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinancialSpaceItem>> GetFinancialSpacesAsync(CancellationToken cancellationToken = default);
    Task AddFinancialSpaceAsync(string name, string ownerName, CancellationToken cancellationToken = default);
    Task RenameFinancialSpaceAsync(Guid spaceId, string name, CancellationToken cancellationToken = default);
    Task DeleteFinancialSpaceAsync(Guid spaceId, Guid? replacementSpaceId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LoginUserItem>> GetLoginUsersAsync(CancellationToken cancellationToken = default);
    Task SetMemberPinAsync(string memberName, string pin, CancellationToken cancellationToken = default);
    Task<bool> AuthenticateMemberAsync(string memberName, string pin, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemberItem>> GetMembersAsync(Guid spaceId, CancellationToken cancellationToken = default);

    Task<SalarySettingsItem?> GetSalarySettingsAsync(Guid memberId, CancellationToken cancellationToken = default);
    Task SaveSalarySettingsAsync(SaveSalarySettingsRequest request, CancellationToken cancellationToken = default);
    Task PostDueSalariesAsync(DateOnly today, CancellationToken cancellationToken = default);

    Task AddMemberAsync(AddMemberRequest request, CancellationToken cancellationToken = default);

    Task UpdateMemberRoleAsync(UpdateMemberRoleRequest request, CancellationToken cancellationToken = default);

    Task DeleteMemberAsync(Guid memberId, CancellationToken cancellationToken = default);

    Task<BackupItem> CreateBackupAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackupItem>> GetBackupsAsync(CancellationToken cancellationToken = default);

    Task RestoreBackupAsync(string backupPath, CancellationToken cancellationToken = default);

    Task<BackupMaintenanceResult> EnsureDailyBackupAsync(CancellationToken cancellationToken = default);
}
