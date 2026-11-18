using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NickFinancas.Application.Finance;
using NickFinancas.Domain.Accounts;
using NickFinancas.Domain.Transactions;
using NickFinancas.Domain.Spaces;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace NickFinancas.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private static readonly CultureInfo BrazilianCulture = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly Guid CurrentUserId = Guid.Parse("619fdf3d-8145-49e3-b1cc-4b6d32b770dd");
    private readonly ILocalFinanceStore _financeStore;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private decimal? _lastAvailableBalance;
    private CancellationTokenSource? _balanceAnimationCancellation;
    private IReadOnlyList<CategoryItem> _allCategories = [];
    private IReadOnlyList<TransactionItem> _allTransactions = [];
    private bool _canReloadPeriod;
    private bool _canReloadSpace;
    private bool _authenticationInitialized;
    private AccountOption? _accountPendingDeletion;
    private CategoryOption? _categoryPendingDeletion;
    private CreditCardOption? _creditCardPendingDeletion;
    private LedgerTransactionViewModel? _transactionPendingDeletion;
    private PayableViewModel? _payablePendingDeletion;
    private RecurringPayableViewModel? _recurringPayablePendingDeletion;
    private InstallmentPlanViewModel? _installmentPlanPendingDeletion;
    private Guid? _editingTransactionId;
    private Guid? _editingCreditCardId;
    private readonly Dictionary<string, (int Year, int Month)> _periods = new();
    private string? _latestBackupPath;
    private Guid? _editingReserveId;
    private Guid? _editingGoalId;
    private ReserveViewModel? _reservePendingDeletion;
    private GoalViewModel? _goalPendingDeletion;
    private MemberViewModel? _memberPendingDeletion;
    private FinancialSpaceOption? _spacePendingDeletion;
    private Guid? _reportCardFilter;
    private string? _reportAuthorFilter;
    private readonly Dictionary<DateOnly, List<CalendarEventViewModel>> _calendarEventsByDate = new();
    private static readonly string ThemePreferencePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NickFinancas", "theme.txt");
    private static readonly string AccentPreferencePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NickFinancas", "accent.txt");
    private static readonly string SessionPreferencePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NickFinancas", "session.txt");

    public MainViewModel(ILocalFinanceStore financeStore)
    {
        _financeStore = financeStore;
        InitializePeriods();
        SelectedTransactionType = TransactionTypes[1];
        SelectedCategoryType = CategoryTypes[1];
        SelectedLedgerType = LedgerTypeFilters[0];
        SelectedLedgerAccount = LedgerAccountFilters[0];
        SelectedLedgerCategory = LedgerCategoryFilters[0];
        SelectedPaymentMethod = PaymentMethods[0];
        TransactionDate = DateTime.Today;
        var savedTheme = File.Exists(ThemePreferencePath) ? File.ReadAllText(ThemePreferencePath).Trim() : "Dark";
        IsDarkTheme = !savedTheme.Equals("Light", StringComparison.OrdinalIgnoreCase);
        var savedAccent = File.Exists(AccentPreferencePath) ? File.ReadAllText(AccentPreferencePath).Trim() : "Blue";
        IsGreenAccent = savedAccent.Equals("Green", StringComparison.OrdinalIgnoreCase);
        ApplyTheme();
    }

    [ObservableProperty] public partial FinancialSpaceOption? SelectedSpace { get; set; }
    [ObservableProperty] public partial Guid? SelectedSpaceId { get; set; }
    [ObservableProperty] public partial string SelectedPeriod { get; set; } = "Setembro de 2026";
    [ObservableProperty] public partial string AvailableBalanceText { get; set; } = "R$ 0,00";
    [ObservableProperty] public partial bool IsBalanceIncreasing { get; set; }
    [ObservableProperty] public partial bool IsBalanceDecreasing { get; set; }
    [ObservableProperty] public partial Thickness BalanceAnimationMargin { get; set; } = new(0, 7, 0, 0);
    [ObservableProperty] public partial double BalanceAnimationOpacity { get; set; } = 1d;
    [ObservableProperty] public partial string MonthlyIncomeText { get; set; } = "R$ 0,00";
    [ObservableProperty] public partial string MonthlyExpenseText { get; set; } = "R$ 0,00";
    [ObservableProperty] public partial bool IsTransactionFormOpen { get; set; }
    [ObservableProperty] public partial bool IsCreatingTransaction { get; set; } = true;
    [ObservableProperty] public partial string TransactionDescription { get; set; } = string.Empty;
    [ObservableProperty] public partial decimal? TransactionAmount { get; set; }
    [ObservableProperty] public partial DateTime? TransactionDate { get; set; }
    [ObservableProperty] public partial AccountOption? SelectedAccount { get; set; }
    [ObservableProperty] public partial AccountOption? SelectedDestinationAccount { get; set; }
    [ObservableProperty] public partial CategoryOption? SelectedCategory { get; set; }
    [ObservableProperty] public partial TransactionTypeOption SelectedTransactionType { get; set; }
    [ObservableProperty] public partial string? FormError { get; set; }
    [ObservableProperty] public partial string? LedgerNotice { get; set; }
    [ObservableProperty] public partial string? LedgerError { get; set; }
    [ObservableProperty] public partial bool IsSaving { get; set; }
    [ObservableProperty] public partial bool IsSettingsOpen { get; set; }
    [ObservableProperty] public partial string NewAccountName { get; set; } = string.Empty;
    [ObservableProperty] public partial decimal NewAccountBalance { get; set; }
    [ObservableProperty] public partial AccountTypeOption SelectedAccountType { get; set; } = AccountTypesList[0];
    [ObservableProperty] public partial string NewCategoryName { get; set; } = string.Empty;
    [ObservableProperty] public partial TransactionTypeOption SelectedCategoryType { get; set; }
    [ObservableProperty] public partial CategoryOption? SelectedParentCategory { get; set; }
    [ObservableProperty] public partial string? SettingsError { get; set; }
    [ObservableProperty] public partial string? SettingsNotice { get; set; }
    [ObservableProperty] public partial decimal? SalaryAmount { get; set; }
    [ObservableProperty] public partial decimal? SalaryPayDay { get; set; }
    [ObservableProperty] public partial AccountOption? SelectedSalaryAccount { get; set; }
    [ObservableProperty] public partial string? SalaryNotice { get; set; }
    [ObservableProperty] public partial string? SalaryError { get; set; }
    [ObservableProperty] public partial bool IsCardsOpen { get; set; }
    [ObservableProperty] public partial string NewCardName { get; set; } = string.Empty;
    [ObservableProperty] public partial decimal? NewCardLimit { get; set; }
    [ObservableProperty] public partial string NewCardClosingDay { get; set; } = "10";
    [ObservableProperty] public partial string NewCardDueDay { get; set; } = "17";
    [ObservableProperty] public partial AccountOption? SelectedCardPaymentAccount { get; set; }
    [ObservableProperty] public partial string? CardError { get; set; }
    [ObservableProperty] public partial string? CardNotice { get; set; }
    [ObservableProperty] public partial bool HasCreditCards { get; set; }
    [ObservableProperty] public partial bool IsCreditCardListEmpty { get; set; } = true;
    [ObservableProperty] public partial string CardFormTitle { get; set; } = "Novo cartao";
    [ObservableProperty] public partial string CardSaveButtonText { get; set; } = "Adicionar cartao";
    [ObservableProperty] public partial bool IsEditingCreditCard { get; set; }
    [ObservableProperty] public partial bool IsDeleteConfirmationOpen { get; set; }
    [ObservableProperty] public partial string DeleteConfirmationText { get; set; } = string.Empty;
    [ObservableProperty] public partial string DeleteConfirmationHint { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsOverviewPage { get; set; } = true;
    [ObservableProperty] public partial bool IsTransactionsPage { get; set; }
    [ObservableProperty] public partial bool IsPayablesPage { get; set; }
    [ObservableProperty] public partial bool IsCalendarPage { get; set; }
    [ObservableProperty] public partial bool IsPlanningPage { get; set; }
    [ObservableProperty] public partial bool IsAccountsPage { get; set; }
    [ObservableProperty] public partial bool IsReservesPage { get; set; }
    [ObservableProperty] public partial bool IsGoalsPage { get; set; }
    [ObservableProperty] public partial bool IsReportsPage { get; set; }
    [ObservableProperty] public partial bool IsMembersPage { get; set; }
    [ObservableProperty] public partial string LedgerSearchText { get; set; } = string.Empty;
    [ObservableProperty] public partial TransactionTypeFilterOption SelectedLedgerType { get; set; }
    [ObservableProperty] public partial AccountFilterOption SelectedLedgerAccount { get; set; }
    [ObservableProperty] public partial CategoryFilterOption SelectedLedgerCategory { get; set; }
    [ObservableProperty] public partial bool HasFilteredTransactions { get; set; }
    [ObservableProperty] public partial bool IsFilteredTransactionListEmpty { get; set; } = true;
    [ObservableProperty] public partial string? LedgerExportNotice { get; set; }
    [ObservableProperty] public partial string? LedgerExportError { get; set; }
    [ObservableProperty] public partial string? LedgerSaveNotice { get; set; }
    [ObservableProperty] public partial bool IsLedgerContextVisible { get; set; }
    [ObservableProperty] public partial string LedgerContextText { get; set; } = string.Empty;
    [ObservableProperty] public partial string TransactionFormTitle { get; set; } = "Novo lancamento";
    [ObservableProperty] public partial string TransactionSaveButtonText { get; set; } = "Salvar lancamento";
    [ObservableProperty] public partial bool IsTransferSelected { get; set; }
    [ObservableProperty] public partial bool IsExpenseSelected { get; set; } = true;
    [ObservableProperty] public partial PaymentMethodOption SelectedPaymentMethod { get; set; }
    [ObservableProperty] public partial bool IsCardPurchase { get; set; }
    [ObservableProperty] public partial bool IsInstallmentPurchase { get; set; }
    [ObservableProperty] public partial decimal? InstallmentCount { get; set; } = 2m;
    [ObservableProperty] public partial decimal? InstallmentStart { get; set; } = 1m;
    [ObservableProperty] public partial CreditCardOption? SelectedPurchaseCard { get; set; }
    [ObservableProperty] public partial bool IsPayableFormOpen { get; set; }
    [ObservableProperty] public partial string PayableDescription { get; set; } = string.Empty;
    [ObservableProperty] public partial decimal? PayableAmount { get; set; }
    [ObservableProperty] public partial DateTime? PayableDueDate { get; set; }
    [ObservableProperty] public partial AccountOption? SelectedPayableAccount { get; set; }
    [ObservableProperty] public partial CategoryOption? SelectedPayableCategory { get; set; }
    [ObservableProperty] public partial bool IsRecurringPayable { get; set; }
    [ObservableProperty] public partial bool IsInstallmentPayable { get; set; }
    [ObservableProperty] public partial decimal? PayableInstallmentCount { get; set; } = 2m;
    [ObservableProperty] public partial decimal? PayableInstallmentStart { get; set; } = 1m;
    [ObservableProperty] public partial string PayableAmountLabel { get; set; } = "Valor";
    [ObservableProperty] public partial string? PayableError { get; set; }
    [ObservableProperty] public partial string? PayableNotice { get; set; }
    [ObservableProperty] public partial string PendingPayablesTotal { get; set; } = "R$ 0,00";
    [ObservableProperty] public partial string PendingPayablesCount { get; set; } = "0 pendencias";
    [ObservableProperty] public partial bool IsPayableListEmpty { get; set; } = true;
    [ObservableProperty] public partial string TotalReservedText { get; set; } = "R$ 0,00";
    [ObservableProperty] public partial string NewReserveName { get; set; } = string.Empty;
    [ObservableProperty] public partial ReserveViewModel? SelectedReserve { get; set; }
    [ObservableProperty] public partial AccountOption? SelectedReserveAccount { get; set; }
    [ObservableProperty] public partial decimal? ReserveMovementAmount { get; set; }
    [ObservableProperty] public partial bool IsReserveDeposit { get; set; } = true;
    [ObservableProperty] public partial ReserveViewModel? SelectedTransferSourceReserve { get; set; }
    [ObservableProperty] public partial ReserveViewModel? SelectedTransferDestinationReserve { get; set; }
    [ObservableProperty] public partial decimal? ReserveTransferAmount { get; set; }
    [ObservableProperty] public partial string? ReserveError { get; set; }
    [ObservableProperty] public partial string? ReserveNotice { get; set; }
    [ObservableProperty] public partial bool IsReserveListEmpty { get; set; } = true;
    [ObservableProperty] public partial bool IsReserveMovementListEmpty { get; set; } = true;
    [ObservableProperty] public partial string ReserveFormTitle { get; set; } = "Nova reserva";
    [ObservableProperty] public partial string ReserveSaveButtonText { get; set; } = "Criar reserva";
    [ObservableProperty] public partial string GoalFormTitle { get; set; } = "Novo objetivo";
    [ObservableProperty] public partial string GoalSaveButtonText { get; set; } = "Criar objetivo";
    [ObservableProperty] public partial bool IsEditingReserve { get; set; }
    [ObservableProperty] public partial bool IsEditingGoal { get; set; }
    [ObservableProperty] public partial string NewGoalName { get; set; } = string.Empty;
    [ObservableProperty] public partial decimal? NewGoalTargetAmount { get; set; }
    [ObservableProperty] public partial decimal? NewGoalMonthlyContribution { get; set; }
    [ObservableProperty] public partial ReserveViewModel? SelectedGoalReserve { get; set; }
    [ObservableProperty] public partial string? GoalError { get; set; }
    [ObservableProperty] public partial string? GoalNotice { get; set; }
    [ObservableProperty] public partial bool IsGoalListEmpty { get; set; } = true;
    [ObservableProperty] public partial bool HasGoals { get; set; }
    [ObservableProperty] public partial bool HasDueAlerts { get; set; }
    [ObservableProperty] public partial string DueAlertText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsOverviewPayablesExpanded { get; set; }
    [ObservableProperty] public partial bool IsOverviewCardLimitsExpanded { get; set; }
    [ObservableProperty] public partial bool IsAccountCardLimitsExpanded { get; set; }
    [ObservableProperty] public partial bool IsPreviousInvoiceExpanded { get; set; }
    [ObservableProperty] public partial bool IsNextInvoiceExpanded { get; set; }
    [ObservableProperty] public partial bool IsOverviewPendingListEmpty { get; set; } = true;
    [ObservableProperty] public partial bool IsAccountListEmpty { get; set; } = true;
    [ObservableProperty] public partial bool IsCategoryListEmpty { get; set; } = true;
    [ObservableProperty] public partial CalendarDayViewModel? SelectedCalendarDay { get; set; }
    [ObservableProperty] public partial string SelectedCalendarDateText { get; set; } = "Selecione um dia";
    [ObservableProperty] public partial string SelectedCalendarSummaryText { get; set; } = "Nenhum compromisso neste dia";
    [ObservableProperty] public partial bool IsCalendarEventListEmpty { get; set; } = true;
    [ObservableProperty] public partial string ProjectionEndingBalanceText { get; set; } = "R$ 0,00";
    [ObservableProperty] public partial string ProjectionAverageIncomeText { get; set; } = "R$ 0,00";
    [ObservableProperty] public partial string ProjectionAverageExpenseText { get; set; } = "R$ 0,00";
    [ObservableProperty] public partial string ProjectionRiskText { get; set; } = "Sem projecao";
    [ObservableProperty] public partial bool HasProjectionRisk { get; set; }
    [ObservableProperty] public partial string NextGoalName { get; set; } = "Nenhum objetivo cadastrado";
    [ObservableProperty] public partial string NextGoalProgressText { get; set; } = "Crie seu primeiro objetivo";
    [ObservableProperty] public partial double NextGoalProgress { get; set; }
    [ObservableProperty] public partial string NextGoalForecastText { get; set; } = "Sem previsao";
    [ObservableProperty] public partial string ReportNetText { get; set; } = "R$ 0,00";
    [ObservableProperty] public partial bool IsReportNetPositive { get; set; }
    [ObservableProperty] public partial bool IsReportNetNegative { get; set; }
    [ObservableProperty] public partial bool IsReportCategoryListEmpty { get; set; } = true;
    [ObservableProperty] public partial string ReportReserveSummary { get; set; } = "R$ 0,00 em 0 reservas";
    [ObservableProperty] public partial string ReportGoalSummary { get; set; } = "0 objetivos";
    [ObservableProperty] public partial string NewMemberName { get; set; } = string.Empty;
    [ObservableProperty] public partial MemberRoleOption SelectedMemberRole { get; set; } = MemberRoleOptionsList[0];
    [ObservableProperty] public partial string? MemberError { get; set; }
    [ObservableProperty] public partial string? MemberNotice { get; set; }
    [ObservableProperty] public partial string NewSpaceName { get; set; } = string.Empty;
    [ObservableProperty] public partial string? SpaceError { get; set; }
    [ObservableProperty] public partial string? SpaceNotice { get; set; }
    [ObservableProperty] public partial bool CanDeleteSelectedSpace { get; set; }
    [ObservableProperty] public partial string MemberCountText { get; set; } = "0 membros";
    [ObservableProperty] public partial MemberViewModel? SelectedActiveMember { get; set; }
    [ObservableProperty] public partial bool IsActiveAdministrator { get; set; } = true;
    [ObservableProperty] public partial string ActivePermissionText { get; set; } = "Acesso administrativo";
    [ObservableProperty] public partial string BackupStatusText { get; set; } = "Nenhum backup local";
    [ObservableProperty] public partial bool HasBackups { get; set; }
    [ObservableProperty] public partial bool IsRestoreConfirmationOpen { get; set; }
    [ObservableProperty] public partial bool IsDarkTheme { get; set; } = true;
    [ObservableProperty] public partial string ThemeButtonText { get; set; } = "Usar tema claro";
    [ObservableProperty] public partial bool IsGreenAccent { get; set; }
    [ObservableProperty] public partial string AccentButtonText { get; set; } = "Cor: azul";
    [ObservableProperty] public partial bool IsLoginOpen { get; set; } = true;
    [ObservableProperty] public partial bool IsAuthenticated { get; set; }
    [ObservableProperty] public partial string AuthenticatedUserName { get; set; } = string.Empty;
    [ObservableProperty] public partial LoginUserOption? SelectedLoginUser { get; set; }
    [ObservableProperty] public partial string LoginPin { get; set; } = string.Empty;
    [ObservableProperty] public partial string LoginPinConfirmation { get; set; } = string.Empty;
    [ObservableProperty] public partial bool RememberLogin { get; set; } = true;
    [ObservableProperty] public partial bool IsCreatingPin { get; set; }
    [ObservableProperty] public partial string LoginTitle { get; set; } = "Entrar no Financeiro";
    [ObservableProperty] public partial string LoginActionText { get; set; } = "Entrar";
    [ObservableProperty] public partial string? LoginError { get; set; }

    public ObservableCollection<FinancialSpaceOption> Spaces { get; } = [];
    public ObservableCollection<LoginUserOption> LoginUsers { get; } = [];
    public ObservableCollection<string> Periods { get; } = [];
    public ObservableCollection<AccountOption> Accounts { get; } = [];
    public ObservableCollection<CategoryOption> Categories { get; } = [];
    public ObservableCollection<TransactionTypeOption> TransactionTypes { get; } =
    [
        new(TransactionType.Income, "Receita"),
        new(TransactionType.Expense, "Despesa"),
        new(TransactionType.Transfer, "Transferencia"),
    ];
    public ObservableCollection<TransactionTypeOption> CategoryTypes { get; } =
    [
        new(TransactionType.Income, "Receita"),
        new(TransactionType.Expense, "Despesa"),
    ];
    private static readonly AccountTypeOption[] AccountTypesList =
    [
        new(FinancialAccountType.Checking, "Conta corrente"),
        new(FinancialAccountType.Savings, "Poupanca"),
        new(FinancialAccountType.Wallet, "Carteira"),
        new(FinancialAccountType.Investment, "Investimento"),
    ];
    public ObservableCollection<AccountTypeOption> AccountTypes { get; } = new(AccountTypesList);
    public ObservableCollection<CategoryOption> AllCategoryOptions { get; } = [];
    public ObservableCollection<CategoryOption> ExpenseCategoryOptions { get; } = [];
    public ObservableCollection<CategoryOption> ParentCategoryOptions { get; } = [];
    public ObservableCollection<RecentTransactionViewModel> RecentTransactions { get; } = [];
    public ObservableCollection<CreditCardOption> CreditCards { get; } = [];
    public ObservableCollection<CreditCardLimitViewModel> CreditCardLimits { get; } = [];
    public ObservableCollection<CreditCardInvoiceViewModel> CreditCardInvoices { get; } = [];
    public ObservableCollection<CreditCardInvoiceViewModel> PreviousCreditCardInvoices { get; } = [];
    public ObservableCollection<CreditCardInvoiceViewModel> CurrentCreditCardInvoices { get; } = [];
    public ObservableCollection<CreditCardInvoiceViewModel> NextCreditCardInvoices { get; } = [];
    public ObservableCollection<CreditCardInvoiceViewModel> PendingCreditCardInvoices { get; } = [];
    public ObservableCollection<PayableViewModel> Payables { get; } = [];
    public ObservableCollection<PayableViewModel> OverviewPendingPayables { get; } = [];
    public ObservableCollection<PayableViewModel> DueSoonPayables { get; } = [];
    public ObservableCollection<RecurringPayableViewModel> RecurringPayables { get; } = [];
    public ObservableCollection<InstallmentPlanViewModel> InstallmentPlans { get; } = [];
    [ObservableProperty] public partial bool HasInstallmentPlans { get; set; }
    public ObservableCollection<CalendarDayViewModel> CalendarDays { get; } = [];
    public ObservableCollection<CalendarEventViewModel> SelectedCalendarEvents { get; } = [];
    public ObservableCollection<FutureProjectionViewModel> FutureProjections { get; } = [];
    public ObservableCollection<ReserveViewModel> Reserves { get; } = [];
    public ObservableCollection<ReserveMovementViewModel> ReserveMovements { get; } = [];
    public ObservableCollection<GoalViewModel> Goals { get; } = [];
    public ObservableCollection<MonthlyReportViewModel> MonthlyReports { get; } = [];
    public ObservableCollection<CategoryReportViewModel> CategoryReports { get; } = [];
    public ObservableCollection<ReportBreakdownViewModel> AccountReports { get; } = [];
    public ObservableCollection<ReportBreakdownViewModel> CardReports { get; } = [];
    public ObservableCollection<ReportBreakdownViewModel> AuthorReports { get; } = [];
    public ObservableCollection<MemberViewModel> Members { get; } = [];
    private static readonly MemberRoleOption[] MemberRoleOptionsList =
    [
        new(FinancialSpaceRole.Member, "Membro"),
        new(FinancialSpaceRole.Administrator, "Administrador"),
    ];
    public ObservableCollection<MemberRoleOption> MemberRoles { get; } = new(MemberRoleOptionsList);
    public ObservableCollection<PaymentMethodOption> PaymentMethods { get; } =
    [
        new(false, "Conta"),
        new(true, "Cartao de credito"),
    ];
    public ObservableCollection<LedgerTransactionViewModel> FilteredTransactions { get; } = [];
    public ObservableCollection<TransactionTypeFilterOption> LedgerTypeFilters { get; } =
    [
        new(null, "Todos os tipos"),
        new(TransactionType.Income, "Receitas"),
        new(TransactionType.Expense, "Despesas"),
        new(TransactionType.Transfer, "Transferencias"),
    ];
    public ObservableCollection<AccountFilterOption> LedgerAccountFilters { get; } = [new(null, "Todas as contas")];
    public ObservableCollection<CategoryFilterOption> LedgerCategoryFilters { get; } = [new(null, "Todas as categorias")];

    public async Task LoadAsync()
    {
        await _loadGate.WaitAsync();
        try
        {
        var loginUsers = await _financeStore.GetLoginUsersAsync();
        var selectedLoginName = SelectedLoginUser?.Name;
        LoginUsers.Clear();
        foreach (var loginUser in loginUsers)
            LoginUsers.Add(new LoginUserOption(loginUser.Name, loginUser.HasPin));
        SelectedLoginUser = LoginUsers.FirstOrDefault(item =>
            item.Name.Equals(selectedLoginName, StringComparison.OrdinalIgnoreCase)) ?? LoginUsers.FirstOrDefault();

        // Restore the session before filtering spaces. Previously this happened near the end of
        // the load, so the first frame used the wrong space and had no active member.
        if (!_authenticationInitialized)
        {
            _authenticationInitialized = true;
            var rememberedUser = File.Exists(SessionPreferencePath) ? File.ReadAllText(SessionPreferencePath).Trim() : string.Empty;
            if (LoginUsers.Any(item => item.Name.Equals(rememberedUser, StringComparison.OrdinalIgnoreCase)))
            {
                AuthenticatedUserName = rememberedUser;
                IsAuthenticated = true;
                IsLoginOpen = false;
            }
        }

        _canReloadSpace = false;
        var selectedSpaceId = SelectedSpace?.Id;
        var financialSpaces = await _financeStore.GetFinancialSpacesAsync();
        var availableSpaces = new List<FinancialSpaceOption>();
        foreach (var space in financialSpaces)
        {
            if (!string.IsNullOrWhiteSpace(AuthenticatedUserName))
            {
                var memberships = await _financeStore.GetMembersAsync(space.Id);
                if (!memberships.Any(member => member.Name.Equals(AuthenticatedUserName, StringComparison.OrdinalIgnoreCase)))
                    continue;
            }
            availableSpaces.Add(new FinancialSpaceOption(space.Id, space.Name, space.IsPrimary));
        }

        // Never show a space without a matching membership. A missing owner is repaired by
        // the store before loading (and after sync); showing it here only leaves controls grey.
        if (!Spaces.SequenceEqual(availableSpaces))
        {
            Spaces.Clear();
            foreach (var space in availableSpaces)
                Spaces.Add(space);
        }
        var spaceToSelect = Spaces.FirstOrDefault(item => item.Id == selectedSpaceId) ?? Spaces.FirstOrDefault();
        SelectedSpaceId = spaceToSelect?.Id;
        SelectedSpace = spaceToSelect;
        CanDeleteSelectedSpace = SelectedSpace is not null && Spaces.Count > 1;
        _canReloadSpace = true;

        if (IsAuthenticated)
            await _financeStore.PostDueSalariesAsync(DateOnly.FromDateTime(DateTime.Today));

        var period = _periods[SelectedPeriod];
        var snapshot = await _financeStore.GetDashboardAsync(period.Year, period.Month);

        AvailableBalanceText = snapshot.AvailableBalance.ToString("C", BrazilianCulture);
        if (_lastAvailableBalance is decimal previousBalance && previousBalance != snapshot.AvailableBalance)
            _ = AnimateAvailableBalanceAsync(snapshot.AvailableBalance > previousBalance);
        _lastAvailableBalance = snapshot.AvailableBalance;
        MonthlyIncomeText = snapshot.MonthlyIncome.ToString("C", BrazilianCulture);
        MonthlyExpenseText = snapshot.MonthlyExpense.ToString("C", BrazilianCulture);
        TotalReservedText = snapshot.TotalReserved.ToString("C", BrazilianCulture);

        var selectedAccountId = SelectedAccount?.Id;
        Accounts.Clear();
        foreach (var account in snapshot.Accounts)
        {
            Accounts.Add(new AccountOption(
                account.Id,
                account.Name,
                GetAccountTypeName(account.Type),
                account.InitialBalance.ToString("C", BrazilianCulture),
                account.CurrentBalance.ToString("C", BrazilianCulture),
                this));
        }
        IsAccountListEmpty = Accounts.Count == 0;

        SelectedAccount = Accounts.FirstOrDefault(account => account.Id == selectedAccountId) ?? Accounts.FirstOrDefault();
        var salaryMember = SelectedSpace is null ? null : (await _financeStore.GetMembersAsync(SelectedSpace.Id))
            .FirstOrDefault(item => item.Name.Equals(AuthenticatedUserName, StringComparison.OrdinalIgnoreCase));
        var salarySettings = salaryMember is null ? null : await _financeStore.GetSalarySettingsAsync(salaryMember.Id);
        SalaryAmount = salarySettings?.Amount;
        SalaryPayDay = salarySettings?.PayDay;
        SelectedSalaryAccount = Accounts.FirstOrDefault(account => account.Id == salarySettings?.AccountId)
            ?? Accounts.FirstOrDefault();
        SelectedDestinationAccount = Accounts.FirstOrDefault(account => account.Id == SelectedDestinationAccount?.Id)
            ?? Accounts.Skip(1).FirstOrDefault();
        SelectedCardPaymentAccount = Accounts.FirstOrDefault(account => account.Id == SelectedCardPaymentAccount?.Id)
            ?? Accounts.FirstOrDefault();
        _allCategories = snapshot.Categories;
        AllCategoryOptions.Clear();
        ExpenseCategoryOptions.Clear();
        foreach (var category in snapshot.Categories.Where(item => item.Type != TransactionType.Transfer))
        {
            var displayName = category.ParentName is null ? category.Name : $"{category.ParentName} › {category.Name}";
            var option = new CategoryOption(category.Id, displayName, GetTransactionTypeName(category.Type), this);
            AllCategoryOptions.Add(option);
            if (category.Type == TransactionType.Expense)
            {
                ExpenseCategoryOptions.Add(option);
            }
        }
        IsCategoryListEmpty = AllCategoryOptions.Count == 0;
        RefreshParentCategories();
        RefreshCategories();

        var selectedLedgerAccountId = SelectedLedgerAccount?.Id;
        LedgerAccountFilters.Clear();
        LedgerAccountFilters.Add(new AccountFilterOption(null, "Todas as contas"));
        foreach (var account in snapshot.Accounts)
        {
            LedgerAccountFilters.Add(new AccountFilterOption(account.Id, account.Name));
        }
        SelectedLedgerAccount = LedgerAccountFilters.FirstOrDefault(item => item.Id == selectedLedgerAccountId)
            ?? LedgerAccountFilters[0];

        var selectedLedgerCategoryId = SelectedLedgerCategory?.Id;
        LedgerCategoryFilters.Clear();
        LedgerCategoryFilters.Add(new CategoryFilterOption(null, "Todas as categorias"));
        foreach (var category in snapshot.Categories)
        {
            LedgerCategoryFilters.Add(new CategoryFilterOption(category.Id,
                category.ParentName is null ? category.Name : $"{category.ParentName} › {category.Name}"));
        }
        SelectedLedgerCategory = LedgerCategoryFilters.FirstOrDefault(item => item.Id == selectedLedgerCategoryId)
            ?? LedgerCategoryFilters[0];

        RecentTransactions.Clear();
        foreach (var transaction in snapshot.RecentTransactions)
        {
            var category = GetCategoryDisplayName(snapshot.Categories.FirstOrDefault(item => item.Id == transaction.CategoryId));
            var account = snapshot.Accounts.FirstOrDefault(item => item.Id == transaction.AccountId)?.Name ?? "Conta";
            var destination = snapshot.Accounts.FirstOrDefault(item => item.Id == transaction.DestinationAccountId)?.Name;
            var signal = transaction.Type is TransactionType.Expense or TransactionType.CreditCardPayment ? "- " : "+ ";
            var detail = transaction.Type == TransactionType.Transfer
                ? $"{account} para {destination ?? "Conta removida"}"
                : $"{category} - {account}";
            var amount = transaction.Type == TransactionType.Transfer
                ? transaction.Amount.ToString("C", BrazilianCulture)
                : signal + transaction.Amount.ToString("C", BrazilianCulture);

            RecentTransactions.Add(new RecentTransactionViewModel(
                transaction.Description,
                detail,
                amount,
                $"{transaction.Date:dd MMM} - {transaction.AuthorName}",
                transaction.Type == TransactionType.Income,
                transaction.Type is TransactionType.Expense or TransactionType.CreditCardPayment));
        }

        _allTransactions = await _financeStore.GetTransactionsAsync(period.Year, period.Month);
        ApplyTransactionFilters();

        var cards = await _financeStore.GetCreditCardsAsync();
        CreditCards.Clear();
        foreach (var card in cards)
        {
            var accountName = Accounts.FirstOrDefault(account => account.Id == card.PaymentAccountId)?.Name ?? "Conta removida";
            CreditCards.Add(new CreditCardOption(
                card.Id,
                card.Name,
                card.PaymentAccountId,
                accountName,
                card.CreditLimit,
                card.CreditLimit.ToString("C", BrazilianCulture),
                card.ClosingDay,
                card.DueDay,
                this));
        }
        HasCreditCards = CreditCards.Count > 0;
        IsCreditCardListEmpty = !HasCreditCards;
        SelectedPurchaseCard = CreditCards.FirstOrDefault(item => item.Id == SelectedPurchaseCard?.Id)
            ?? CreditCards.FirstOrDefault();

        var currentReference = new DateOnly(period.Year, period.Month, 1);
        var previousReference = currentReference.AddMonths(-1);
        var nextReference = currentReference.AddMonths(1);
        var invoiceGroups = await Task.WhenAll(
            _financeStore.GetCreditCardInvoicesAsync(previousReference.Year, previousReference.Month),
            _financeStore.GetCreditCardInvoicesAsync(currentReference.Year, currentReference.Month),
            _financeStore.GetCreditCardInvoicesAsync(nextReference.Year, nextReference.Month));
        CreditCardInvoices.Clear();
        CreditCardLimits.Clear();
        PreviousCreditCardInvoices.Clear();
        CurrentCreditCardInvoices.Clear();
        NextCreditCardInvoices.Clear();
        PendingCreditCardInvoices.Clear();
        for (var groupIndex = 0; groupIndex < invoiceGroups.Length; groupIndex++)
        {
            var target = groupIndex == 0 ? PreviousCreditCardInvoices :
                groupIndex == 1 ? CurrentCreditCardInvoices : NextCreditCardInvoices;
            foreach (var invoice in invoiceGroups[groupIndex])
            {
                var viewModel = CreateInvoiceViewModel(invoice);
                target.Add(viewModel);
                CreditCardInvoices.Add(viewModel);
                if (groupIndex == 1 && invoice.Outstanding > 0m)
                {
                    PendingCreditCardInvoices.Add(viewModel);
                }
            }
        }
        foreach (var invoice in invoiceGroups[1])
        {
            CreditCardLimits.Add(new CreditCardLimitViewModel(
                invoice.CreditCardId,
                invoice.CardName,
                invoice.CreditLimit,
                invoice.MaximumLimit,
                invoice.UsedLimit,
                this));
        }
        ApplyTransactionFilters();

        var payables = await _financeStore.GetPayablesAsync(period.Year, period.Month);
        Payables.Clear();
        OverviewPendingPayables.Clear();
        foreach (var payable in payables)
        {
            var accountName = Accounts.FirstOrDefault(item => item.Id == payable.AccountId)?.Name ?? "Conta removida";
            var categoryName = AllCategoryOptions.FirstOrDefault(item => item.Id == payable.CategoryId)?.Name ?? "Sem categoria";
            var payableViewModel = new PayableViewModel(
                payable.Id, payable.Description, payable.Amount.ToString("C", BrazilianCulture), payable.Amount,
                payable.DueDate.ToString("dd/MM/yyyy", BrazilianCulture), payable.Status,
                payable.IsRecurring, payable.PaidDate is null, accountName, categoryName, this);
            Payables.Add(payableViewModel);
            if (payable.PaidDate is null && OverviewPendingPayables.Count < 5)
                OverviewPendingPayables.Add(payableViewModel);
        }
        IsPayableListEmpty = Payables.Count == 0;
        var installmentPlans = await _financeStore.GetInstallmentPlansAsync();
        InstallmentPlans.Clear();
        foreach (var plan in installmentPlans)
            InstallmentPlans.Add(new InstallmentPlanViewModel(
                plan.GroupId, plan.Description, plan.PaymentMethod,
                $"{plan.RemainingCount} de {plan.TotalCount} parcelas restantes",
                plan.NextDueDate.ToString("dd/MM/yyyy", BrazilianCulture),
                plan.NextAmount.ToString("C", BrazilianCulture),
                plan.IsAccountPayable, this));
        HasInstallmentPlans = InstallmentPlans.Count > 0;
        var pending = payables.Where(item => item.PaidDate is null).ToList();
        var pendingInvoices = invoiceGroups[1].Where(item => item.Outstanding > 0m).ToList();
        var totalPendingCount = pending.Count + pendingInvoices.Count;
        PendingPayablesTotal = (pending.Sum(item => item.Amount) + pendingInvoices.Sum(item => item.Outstanding))
            .ToString("C", BrazilianCulture);
        PendingPayablesCount = totalPendingCount == 1 ? "1 pendencia" : $"{totalPendingCount} pendencias";
        IsOverviewPendingListEmpty = totalPendingCount == 0;
        DueSoonPayables.Clear();
        var today = DateOnly.FromDateTime(DateTime.Today);
        foreach (var payable in payables.Where(item => item.PaidDate is null && item.DueDate >= today && item.DueDate <= today.AddDays(7)))
        {
            var viewModel = Payables.First(item => item.Id == payable.Id);
            DueSoonPayables.Add(viewModel);
        }
        HasDueAlerts = DueSoonPayables.Count > 0;
        DueAlertText = DueSoonPayables.Count == 1
            ? "1 conta vence nos proximos 7 dias"
            : $"{DueSoonPayables.Count} contas vencem nos proximos 7 dias";
        var recurringItems = await _financeStore.GetRecurringPayablesAsync();
        RecurringPayables.Clear();
        foreach (var recurring in recurringItems)
        {
            var accountName = Accounts.FirstOrDefault(item => item.Id == recurring.AccountId)?.Name ?? "Conta removida";
            RecurringPayables.Add(new RecurringPayableViewModel(
                recurring.Id, recurring.Description, recurring.Amount.ToString("C", BrazilianCulture),
                $"Todo dia {recurring.DueDay}", accountName, this));
        }
        BuildCalendar(period.Year, period.Month, payables, invoiceGroups[1]);

        var selectedReserveId = SelectedReserve?.Id;
        var reserves = await _financeStore.GetReservesAsync();
        Reserves.Clear();
        foreach (var reserve in reserves)
            Reserves.Add(new ReserveViewModel(reserve.Id, reserve.Name, reserve.Balance,
                reserve.Balance.ToString("C", BrazilianCulture), this));
        SelectedReserve = Reserves.FirstOrDefault(item => item.Id == selectedReserveId) ?? Reserves.FirstOrDefault();
        SelectedReserveAccount = Accounts.FirstOrDefault(item => item.Id == SelectedReserveAccount?.Id) ?? Accounts.FirstOrDefault();
        SelectedTransferSourceReserve = Reserves.FirstOrDefault(item => item.Id == SelectedTransferSourceReserve?.Id) ?? Reserves.FirstOrDefault();
        SelectedTransferDestinationReserve = Reserves.FirstOrDefault(item => item.Id == SelectedTransferDestinationReserve?.Id && item.Id != SelectedTransferSourceReserve?.Id)
            ?? Reserves.FirstOrDefault(item => item.Id != SelectedTransferSourceReserve?.Id);
        IsReserveListEmpty = Reserves.Count == 0;
        var reserveMovements = await _financeStore.GetReserveMovementsAsync();
        ReserveMovements.Clear();
        foreach (var movement in reserveMovements)
        {
            var reserveName = Reserves.FirstOrDefault(item => item.Id == movement.ReserveId)?.Name ?? "Reserva removida";
            var accountName = Accounts.FirstOrDefault(item => item.Id == movement.AccountId)?.Name ?? "Conta removida";
            ReserveMovements.Add(new ReserveMovementViewModel(
                reserveName, accountName, movement.IsDeposit ? "Aporte" : "Retirada",
                (movement.IsDeposit ? "+ " : "- ") + movement.Amount.ToString("C", BrazilianCulture),
                movement.Date.ToString("dd/MM/yyyy", BrazilianCulture), movement.IsDeposit));
        }
        IsReserveMovementListEmpty = ReserveMovements.Count == 0;
        SelectedGoalReserve = Reserves.FirstOrDefault(item => item.Id == SelectedGoalReserve?.Id) ?? Reserves.FirstOrDefault();

        var goals = await _financeStore.GetGoalsAsync();
        Goals.Clear();
        foreach (var goal in goals)
            Goals.Add(CreateGoalViewModel(goal));
        HasGoals = Goals.Count > 0;
        IsGoalListEmpty = !HasGoals;
        var nextGoal = Goals.FirstOrDefault(item => item.IsPrimary);
        NextGoalName = nextGoal?.Name ?? "Nenhum objetivo cadastrado";
        NextGoalProgressText = nextGoal?.ProgressText ?? "Crie seu primeiro objetivo";
        NextGoalProgress = nextGoal?.ProgressPercentage ?? 0d;
        NextGoalForecastText = nextGoal?.ForecastText ?? "Sem previsao";

        var reportReferences = Enumerable.Range(0, 6).Select(offset => currentReference.AddMonths(offset - 5)).ToList();
        var reportSnapshots = await Task.WhenAll(reportReferences.Select(reference =>
            _financeStore.GetDashboardAsync(reference.Year, reference.Month)));
        var reportMaximum = Math.Max(1m, reportSnapshots.Max(item => Math.Max(item.MonthlyIncome, item.MonthlyExpense)));
        MonthlyReports.Clear();
        for (var index = 0; index < reportReferences.Count; index++)
        {
            var reference = reportReferences[index];
            var report = reportSnapshots[index];
            MonthlyReports.Add(new MonthlyReportViewModel(
                BrazilianCulture.TextInfo.ToTitleCase(GetMonthName(reference.Month))[..3],
                report.MonthlyIncome.ToString("C", BrazilianCulture), report.MonthlyExpense.ToString("C", BrazilianCulture),
                (double)(report.MonthlyIncome / reportMaximum * 120m),
                (double)(report.MonthlyExpense / reportMaximum * 120m)));
        }
        var reportNet = snapshot.MonthlyIncome - snapshot.MonthlyExpense;
        ReportNetText = reportNet.ToString("C", BrazilianCulture);
        IsReportNetPositive = reportNet >= 0m;
        IsReportNetNegative = reportNet < 0m;
        CategoryReports.Clear();
        foreach (var group in _allTransactions.Where(item => item.Type == TransactionType.Expense)
                     .GroupBy(item => item.CategoryId).OrderByDescending(group => group.Sum(item => item.Amount)))
        {
            var amount = group.Sum(item => item.Amount);
            CategoryReports.Add(new CategoryReportViewModel(
                group.Key,
                GetCategoryDisplayName(snapshot.Categories.FirstOrDefault(item => item.Id == group.Key)),
                amount.ToString("C", BrazilianCulture),
                snapshot.MonthlyExpense <= 0m ? 0d : (double)(amount / snapshot.MonthlyExpense * 100m),
                $"{Math.Round(snapshot.MonthlyExpense <= 0m ? 0m : amount / snapshot.MonthlyExpense * 100m):N0}%"));
        }
        IsReportCategoryListEmpty = CategoryReports.Count == 0;
        AccountReports.Clear();
        foreach (var group in _allTransactions.Where(item => item.Type == TransactionType.Expense && item.CreditCardId is null)
                     .GroupBy(item => item.AccountId).OrderByDescending(group => group.Sum(item => item.Amount)))
        {
            var accountName = Accounts.FirstOrDefault(item => item.Id == group.Key)?.Name ?? "Conta removida";
            AccountReports.Add(new ReportBreakdownViewModel(ReportDrilldownKind.Account, group.Key, null, accountName,
                group.Sum(item => item.Amount).ToString("C", BrazilianCulture), $"{group.Count()} lancamentos"));
        }
        CardReports.Clear();
        foreach (var group in _allTransactions.Where(item => item.Type == TransactionType.Expense && item.CreditCardId is not null)
                     .GroupBy(item => item.CreditCardId!.Value).OrderByDescending(group => group.Sum(item => item.Amount)))
        {
            var cardName = CreditCards.FirstOrDefault(item => item.Id == group.Key)?.Name ?? "Cartao removido";
            CardReports.Add(new ReportBreakdownViewModel(ReportDrilldownKind.Card, group.Key, null, cardName,
                group.Sum(item => item.Amount).ToString("C", BrazilianCulture), $"{group.Count()} compras"));
        }
        AuthorReports.Clear();
        foreach (var group in _allTransactions.Where(item => item.Type is TransactionType.Income or TransactionType.Expense)
                     .GroupBy(item => item.AuthorName).OrderByDescending(group => group.Sum(item => item.Amount)))
            AuthorReports.Add(new ReportBreakdownViewModel(ReportDrilldownKind.Author, null, group.Key, group.Key,
                group.Sum(item => item.Amount).ToString("C", BrazilianCulture), $"{group.Count()} lancamentos"));
        ReportReserveSummary = $"{snapshot.TotalReserved.ToString("C", BrazilianCulture)} em {Reserves.Count} reservas";
        ReportGoalSummary = Goals.Count == 1 ? "1 objetivo" : $"{Goals.Count} objetivos";
        await BuildFuturePlanningAsync(snapshot, currentReference, reportSnapshots);

        var members = SelectedSpace is null
            ? []
            : await _financeStore.GetMembersAsync(SelectedSpace.Id);
        Members.Clear();
        foreach (var member in members)
            Members.Add(new MemberViewModel(member.Id, member.Name, member.Role,
                member.Role == FinancialSpaceRole.Administrator ? "Administrador" : "Membro",
                !member.IsPrimary, this));
        MemberCountText = Members.Count == 1 ? "1 membro" : $"{Members.Count} membros";
        SelectedActiveMember = IsAuthenticated
            ? Members.FirstOrDefault(item => item.Name.Equals(AuthenticatedUserName, StringComparison.OrdinalIgnoreCase))
            : null;
        IsActiveAdministrator = SelectedActiveMember?.Role == FinancialSpaceRole.Administrator;
        ActivePermissionText = !IsAuthenticated ? "Sessao bloqueada"
            : IsActiveAdministrator ? "Acesso administrativo" : "Acesso de membro";

        var backups = await _financeStore.GetBackupsAsync();
        var latestBackup = backups.FirstOrDefault();
        _latestBackupPath = latestBackup?.Path;
        HasBackups = latestBackup is not null;
        BackupStatusText = latestBackup is null
            ? "Nenhum backup local"
            : $"Ultimo: {latestBackup.CreatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm} ({latestBackup.SizeBytes / 1024d / 1024d:N1} MB) • {backups.Count}/10 copias";

        _canReloadPeriod = true;
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private async Task AnimateAvailableBalanceAsync(bool increased)
    {
        _balanceAnimationCancellation?.Cancel();
        _balanceAnimationCancellation?.Dispose();
        _balanceAnimationCancellation = new CancellationTokenSource();
        var cancellationToken = _balanceAnimationCancellation.Token;

        IsBalanceIncreasing = false;
        IsBalanceDecreasing = false;
        try
        {
            await Task.Delay(1, cancellationToken);
            IsBalanceIncreasing = increased;
            IsBalanceDecreasing = !increased;
            BalanceAnimationMargin = increased
                ? new Thickness(0, -3, 0, 10)
                : new Thickness(0, 17, 0, -10);
            BalanceAnimationOpacity = 0.35d;
            await Task.Delay(180, cancellationToken);
            BalanceAnimationMargin = new Thickness(0, 7, 0, 0);
            BalanceAnimationOpacity = 1d;
            await Task.Delay(320, cancellationToken);
            IsBalanceIncreasing = false;
            IsBalanceDecreasing = false;
        }
        catch (OperationCanceledException)
        {
            // A newer balance change owns the visual state now.
        }
    }

    [RelayCommand]
    private void OpenTransactionForm()
    {
        _editingTransactionId = null;
        IsCreatingTransaction = true;
        TransactionFormTitle = "Novo lancamento";
        TransactionSaveButtonText = "Salvar lancamento";
        TransactionDescription = string.Empty;
        TransactionAmount = null;
        var period = _periods[SelectedPeriod];
        var today = DateTime.Today;
        TransactionDate = today.Year == period.Year && today.Month == period.Month
            ? today
            : new DateTime(period.Year, period.Month,
                Math.Min(today.Day, DateTime.DaysInMonth(period.Year, period.Month)));
        FormError = null;
        LedgerSaveNotice = null;
        SelectedAccount ??= Accounts.FirstOrDefault();
        SelectedDestinationAccount = Accounts.FirstOrDefault(item => item.Id != SelectedAccount?.Id);
        SelectedPaymentMethod = PaymentMethods[0];
        SelectedPurchaseCard = CreditCards.FirstOrDefault();
        InstallmentCount = 2m;
        InstallmentStart = 1m;
        IsInstallmentPurchase = false;
        IsTransactionFormOpen = true;
    }

    [RelayCommand] private void ToggleOverviewPayables() => IsOverviewPayablesExpanded = !IsOverviewPayablesExpanded;
    [RelayCommand] private void ToggleOverviewCardLimits() => IsOverviewCardLimitsExpanded = !IsOverviewCardLimitsExpanded;
    [RelayCommand] private void ToggleAccountCardLimits() => IsAccountCardLimitsExpanded = !IsAccountCardLimitsExpanded;
    [RelayCommand] private void TogglePreviousInvoice() => IsPreviousInvoiceExpanded = !IsPreviousInvoiceExpanded;
    [RelayCommand] private void ToggleNextInvoice() => IsNextInvoiceExpanded = !IsNextInvoiceExpanded;

    [RelayCommand]
    private void EditTransaction(LedgerTransactionViewModel transaction)
    {
        _editingTransactionId = transaction.Id;
        IsCreatingTransaction = false;
        TransactionFormTitle = "Editar lancamento";
        TransactionSaveButtonText = "Salvar alteracoes";
        TransactionDescription = transaction.Description;
        TransactionAmount = transaction.RawAmount;
        TransactionDate = transaction.Date.ToDateTime(TimeOnly.MinValue);
        SelectedAccount = Accounts.FirstOrDefault(item => item.Id == transaction.AccountId);
        SelectedTransactionType = TransactionTypes.First(item => item.Type == transaction.Type);
        SelectedDestinationAccount = Accounts.FirstOrDefault(item => item.Id == transaction.DestinationAccountId);
        SelectedPaymentMethod = PaymentMethods[transaction.CreditCardId is null ? 0 : 1];
        SelectedPurchaseCard = CreditCards.FirstOrDefault(item => item.Id == transaction.CreditCardId);
        InstallmentCount = 2m;
        InstallmentStart = 1m;
        IsInstallmentPurchase = false;
        SelectedCategory = Categories.FirstOrDefault(item => item.Id == transaction.CategoryId);
        FormError = null;
        IsTransactionFormOpen = true;
    }

    [RelayCommand]
    private void Navigate(string page)
    {
        if (page == "Transactions") ResetReportDrilldown();
        IsOverviewPage = page == "Overview";
        IsTransactionsPage = page == "Transactions";
        IsPayablesPage = page == "Payables";
        IsCalendarPage = page == "Calendar";
        IsPlanningPage = page == "Planning";
        IsAccountsPage = page == "Accounts";
        IsReservesPage = page == "Reserves";
        IsGoalsPage = page == "Goals";
        IsReportsPage = page == "Reports";
        IsMembersPage = page == "Members";
    }

    [RelayCommand]
    private void SelectCalendarDay(CalendarDayViewModel day)
    {
        if (day.Date is not DateOnly date) return;
        SelectedCalendarDay = day;
        SelectedCalendarDateText = BrazilianCulture.TextInfo.ToTitleCase(
            date.ToDateTime(TimeOnly.MinValue).ToString("dddd, dd 'de' MMMM", BrazilianCulture));
        SelectedCalendarEvents.Clear();
        if (_calendarEventsByDate.TryGetValue(date, out var events))
            foreach (var item in events) SelectedCalendarEvents.Add(item);
        IsCalendarEventListEmpty = SelectedCalendarEvents.Count == 0;
        SelectedCalendarSummaryText = IsCalendarEventListEmpty
            ? "Nenhum compromisso neste dia"
            : SelectedCalendarEvents.Count == 1 ? "1 compromisso programado" : $"{SelectedCalendarEvents.Count} compromissos programados";
    }

    [RelayCommand]
    private void ScheduleFromCalendar()
    {
        var date = SelectedCalendarDay?.Date ?? DateOnly.FromDateTime(DateTime.Today);
        Navigate("Payables");
        OpenPayableForm();
        PayableDueDate = date.ToDateTime(TimeOnly.MinValue);
    }

    private void BuildCalendar(
        int year,
        int month,
        IReadOnlyList<PayableItem> payables,
        IReadOnlyList<CreditCardInvoiceItem> invoices)
    {
        _calendarEventsByDate.Clear();
        void AddEvent(DateOnly date, CalendarEventViewModel item)
        {
            if (!_calendarEventsByDate.TryGetValue(date, out var events))
                _calendarEventsByDate[date] = events = [];
            events.Add(item);
        }

        foreach (var transaction in _allTransactions)
        {
            var kind = transaction.Type switch
            {
                TransactionType.Income => "Receita",
                TransactionType.Expense => "Despesa",
                TransactionType.Transfer => "Transferencia",
                _ => "Pagamento de fatura",
            };
            var signal = transaction.Type == TransactionType.Income ? "+ " :
                transaction.Type is TransactionType.Expense or TransactionType.CreditCardPayment ? "- " : string.Empty;
            var accountName = Accounts.FirstOrDefault(item => item.Id == transaction.AccountId)?.Name ?? "Conta removida";
            var categoryName = AllCategoryOptions.FirstOrDefault(item => item.Id == transaction.CategoryId)?.Name ?? "Sem categoria";
            AddEvent(transaction.Date, new CalendarEventViewModel(
                transaction.Description, kind, $"{accountName} • {categoryName} • por {transaction.AuthorName}",
                signal + transaction.Amount.ToString("C", BrazilianCulture), "Registrado",
                transaction.Type == TransactionType.Income, false));
        }
        foreach (var payable in payables)
        {
            var accountName = Accounts.FirstOrDefault(item => item.Id == payable.AccountId)?.Name ?? "Conta removida";
            var categoryName = AllCategoryOptions.FirstOrDefault(item => item.Id == payable.CategoryId)?.Name ?? "Sem categoria";
            AddEvent(payable.DueDate, new CalendarEventViewModel(
                payable.Description, payable.PaidDate is null ? "Conta a vencer" : "Conta paga",
                $"{accountName} • {categoryName}{(payable.IsRecurring ? " • recorrente" : string.Empty)}",
                payable.Amount.ToString("C", BrazilianCulture), payable.PaidDate is null ? "Pendente" : "Pago",
                false, payable.PaidDate is null));
        }
        foreach (var invoice in invoices.Where(item => item.Outstanding > 0m))
            AddEvent(invoice.DueDate, new CalendarEventViewModel(
                $"Fatura {invoice.CardName}", "Fatura a vencer",
                $"Cartao de credito • fechamento em {invoice.ClosingDate:dd/MM/yyyy}",
                invoice.Outstanding.ToString("C", BrazilianCulture), "Pendente", false, true));

        CalendarDays.Clear();
        var first = new DateOnly(year, month, 1);
        var leading = ((int)first.DayOfWeek + 6) % 7;
        for (var index = 0; index < leading; index++)
            CalendarDays.Add(new CalendarDayViewModel(null, string.Empty, false, false, string.Empty, string.Empty));
        var today = DateOnly.FromDateTime(DateTime.Today);
        for (var day = 1; day <= DateTime.DaysInMonth(year, month); day++)
        {
            var date = new DateOnly(year, month, day);
            var dayEvents = _calendarEventsByDate.GetValueOrDefault(date) ?? [];
            var count = dayEvents.Count;
            var preview = string.Join(Environment.NewLine, dayEvents.Take(2).Select(item => $"{item.Kind}: {item.Description}  {item.AmountText}"));
            if (count > 2) preview += $"{Environment.NewLine}+ {count - 2} outro(s)";
            CalendarDays.Add(new CalendarDayViewModel(date, day.ToString(BrazilianCulture), date == today,
                count > 0, count == 0 ? string.Empty : count == 1 ? "1 compromisso" : $"{count} compromissos", preview));
        }
        while (CalendarDays.Count % 7 != 0)
            CalendarDays.Add(new CalendarDayViewModel(null, string.Empty, false, false, string.Empty, string.Empty));

        var selectedDate = SelectedCalendarDay?.Date;
        var selected = CalendarDays.FirstOrDefault(item => item.Date == selectedDate)
            ?? CalendarDays.FirstOrDefault(item => item.Date == today)
            ?? CalendarDays.FirstOrDefault(item => item.Date is not null);
        if (selected is not null) SelectCalendarDay(selected);
    }

    private async Task BuildFuturePlanningAsync(
        DashboardSnapshot currentSnapshot,
        DateOnly currentReference,
        IReadOnlyList<DashboardSnapshot> historicalSnapshots)
    {
        var activeHistory = historicalSnapshots
            .Where(item => item.MonthlyIncome > 0m || item.MonthlyExpense > 0m)
            .ToList();
        if (activeHistory.Count == 0) activeHistory.Add(currentSnapshot);

        var averageIncome = activeHistory.Average(item => item.MonthlyIncome);
        var averageExpense = activeHistory.Average(item => item.MonthlyExpense);
        var monthlyGoalContributions = Goals.Sum(item => item.PlannedMonthlyContribution);
        ProjectionAverageIncomeText = averageIncome.ToString("C", BrazilianCulture);
        ProjectionAverageExpenseText = averageExpense.ToString("C", BrazilianCulture);

        var references = Enumerable.Range(1, 6).Select(currentReference.AddMonths).ToList();
        var inputs = await Task.WhenAll(references.Select(LoadProjectionMonthAsync));
        FutureProjections.Clear();
        var openingBalance = currentSnapshot.AvailableBalance;
        for (var index = 0; index < references.Count; index++)
        {
            var reference = references[index];
            var (futureSnapshot, payables, invoices) = inputs[index];
            var income = futureSnapshot.MonthlyIncome > 0m ? futureSnapshot.MonthlyIncome : averageIncome;
            var variableExpenses = futureSnapshot.MonthlyExpense > 0m ? futureSnapshot.MonthlyExpense : averageExpense;
            var knownCommitments = payables.Where(item => item.PaidDate is null).Sum(item => item.Amount)
                + invoices.Where(item => item.Outstanding > 0m).Sum(item => item.Outstanding);
            var endingBalance = openingBalance + income - variableExpenses - knownCommitments - monthlyGoalContributions;
            var isRisk = endingBalance < 0m;
            FutureProjections.Add(new FutureProjectionViewModel(
                BrazilianCulture.TextInfo.ToTitleCase($"{GetMonthName(reference.Month)} {reference.Year}"),
                openingBalance.ToString("C", BrazilianCulture), income.ToString("C", BrazilianCulture),
                variableExpenses.ToString("C", BrazilianCulture), knownCommitments.ToString("C", BrazilianCulture),
                monthlyGoalContributions.ToString("C", BrazilianCulture), endingBalance.ToString("C", BrazilianCulture),
                isRisk ? "Atencao: saldo negativo" : "Saldo previsto positivo", isRisk));
            openingBalance = endingBalance;
        }

        ProjectionEndingBalanceText = openingBalance.ToString("C", BrazilianCulture);
        var riskCount = FutureProjections.Count(item => item.IsRisk);
        HasProjectionRisk = riskCount > 0;
        ProjectionRiskText = riskCount == 0 ? "Cenario sustentavel nos proximos 6 meses"
            : riskCount == 1 ? "1 mes com risco de saldo negativo" : $"{riskCount} meses com risco de saldo negativo";
    }

    private async Task<(DashboardSnapshot Snapshot, IReadOnlyList<PayableItem> Payables,
        IReadOnlyList<CreditCardInvoiceItem> Invoices)> LoadProjectionMonthAsync(DateOnly reference)
    {
        var snapshotTask = _financeStore.GetDashboardAsync(reference.Year, reference.Month);
        var payablesTask = _financeStore.GetPayablesAsync(reference.Year, reference.Month);
        var invoicesTask = _financeStore.GetCreditCardInvoicesAsync(reference.Year, reference.Month);
        await Task.WhenAll(snapshotTask, payablesTask, invoicesTask);
        return (await snapshotTask, await payablesTask, await invoicesTask);
    }

    [RelayCommand]
    private void OpenReportTransactions()
    {
        Navigate("Transactions");
        SelectedLedgerType = LedgerTypeFilters.First(item => item.Type == TransactionType.Expense);
    }

    [RelayCommand]
    private async Task ExportTransactionsAsync()
    {
        try
        {
            var period = _periods[SelectedPeriod];
            var result = await _financeStore.ExportTransactionsCsvAsync(period.Year, period.Month);
            LedgerExportError = null;
            LedgerExportNotice = result.TransactionCount == 1
                ? $"1 lancamento exportado para {result.Path}"
                : $"{result.TransactionCount} lancamentos exportados para {result.Path}";
        }
        catch (Exception exception)
        {
            LedgerExportNotice = null;
            LedgerExportError = $"Nao foi possivel exportar: {exception.Message}";
        }
    }

    [RelayCommand]
    private void OpenCategoryReport(CategoryReportViewModel report)
    {
        Navigate("Transactions");
        SelectedLedgerType = LedgerTypeFilters.First(item => item.Type == TransactionType.Expense);
        SelectedLedgerCategory = LedgerCategoryFilters.FirstOrDefault(item => item.Id == report.CategoryId)
            ?? LedgerCategoryFilters[0];
        ShowReportContext($"Despesas da categoria {report.Name}");
    }

    [RelayCommand]
    private void OpenReportBreakdown(ReportBreakdownViewModel report)
    {
        Navigate("Transactions");
        if (report.Kind != ReportDrilldownKind.Author)
            SelectedLedgerType = LedgerTypeFilters.First(item => item.Type == TransactionType.Expense);
        if (report.Kind == ReportDrilldownKind.Account)
            SelectedLedgerAccount = LedgerAccountFilters.FirstOrDefault(item => item.Id == report.Id)
                ?? LedgerAccountFilters[0];
        else if (report.Kind == ReportDrilldownKind.Card)
            _reportCardFilter = report.Id;
        else if (report.Kind == ReportDrilldownKind.Author)
            _reportAuthorFilter = report.Value;
        ShowReportContext($"Detalhamento: {report.Name}");
        ApplyTransactionFilters();
    }

    private void ResetReportDrilldown()
    {
        _reportCardFilter = null;
        _reportAuthorFilter = null;
        LedgerSearchText = string.Empty;
        if (LedgerTypeFilters.Count > 0) SelectedLedgerType = LedgerTypeFilters[0];
        if (LedgerAccountFilters.Count > 0) SelectedLedgerAccount = LedgerAccountFilters[0];
        if (LedgerCategoryFilters.Count > 0) SelectedLedgerCategory = LedgerCategoryFilters[0];
        IsLedgerContextVisible = false;
        LedgerContextText = string.Empty;
    }

    private void ShowReportContext(string text)
    {
        LedgerSearchText = string.Empty;
        LedgerContextText = text;
        IsLedgerContextVisible = true;
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        ApplyTheme();
        Directory.CreateDirectory(Path.GetDirectoryName(ThemePreferencePath)!);
        File.WriteAllText(ThemePreferencePath, IsDarkTheme ? "Dark" : "Light");
    }

    [RelayCommand]
    private void ToggleAccent()
    {
        IsGreenAccent = !IsGreenAccent;
        ApplyAccent();
        Directory.CreateDirectory(Path.GetDirectoryName(AccentPreferencePath)!);
        File.WriteAllText(AccentPreferencePath, IsGreenAccent ? "Green" : "Blue");
    }

    private void ApplyTheme()
    {
        if (Avalonia.Application.Current is not null)
            Avalonia.Application.Current.RequestedThemeVariant = IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        ThemeButtonText = IsDarkTheme ? "Escuro" : "Claro";
        ApplyAccent();
    }

    private void ApplyAccent()
    {
        AccentButtonText = IsGreenAccent ? "Verde" : "Azul";
        if (Avalonia.Application.Current is null) return;
        var palette = (IsGreenAccent, IsDarkTheme) switch
        {
            (true, true) => new[] { "#07150F", "#0D2118", "#153326", "#06110C", "#ECFDF5", "#9BBCAA", "#26523D", "#34D399" },
            (true, false) => new[] { "#F3F9F5", "#FFFFFF", "#E8F5ED", "#123524", "#17251D", "#617568", "#CEE4D6", "#059669" },
            (false, true) => new[] { "#0B1220", "#111C2E", "#1A2940", "#07101F", "#EFF6FF", "#94A3B8", "#293B55", "#60A5FA" },
            _ => new[] { "#F4F7FB", "#FFFFFF", "#EDF3FA", "#14243A", "#162033", "#627086", "#D7E1EE", "#2563EB" },
        };
        var keys = new[] { "AppBackgroundColor", "SurfaceColor", "SurfaceAltColor", "SidebarColor", "TextColor", "MutedTextColor", "BorderColor", "AccentColor" };
        for (var index = 0; index < keys.Length; index++)
            Avalonia.Application.Current.Resources[keys[index]] = Color.Parse(palette[index]);
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        try
        {
            if (SelectedLoginUser is null) throw new InvalidOperationException("Selecione seu nome.");
            if (IsCreatingPin)
            {
                if (LoginPin != LoginPinConfirmation)
                    throw new InvalidOperationException("A confirmacao do PIN nao confere.");
                await _financeStore.SetMemberPinAsync(SelectedLoginUser.Name, LoginPin);
            }
            else if (!await _financeStore.AuthenticateMemberAsync(SelectedLoginUser.Name, LoginPin))
            {
                throw new InvalidOperationException("PIN incorreto.");
            }

            AuthenticatedUserName = SelectedLoginUser.Name;
            IsAuthenticated = true;
            IsLoginOpen = false;
            LoginPin = string.Empty;
            LoginPinConfirmation = string.Empty;
            LoginError = null;
            Directory.CreateDirectory(Path.GetDirectoryName(SessionPreferencePath)!);
            if (RememberLogin)
                File.WriteAllText(SessionPreferencePath, AuthenticatedUserName);
            else if (File.Exists(SessionPreferencePath))
                File.Delete(SessionPreferencePath);
            await LoadAsync();
        }
        catch (Exception exception) { LoginError = exception.Message; }
    }

    [RelayCommand]
    private void Logout()
    {
        if (File.Exists(SessionPreferencePath)) File.Delete(SessionPreferencePath);
        IsAuthenticated = false;
        AuthenticatedUserName = string.Empty;
        SelectedActiveMember = null;
        IsActiveAdministrator = false;
        ActivePermissionText = "Sessao bloqueada";
        LoginPin = string.Empty;
        LoginPinConfirmation = string.Empty;
        LoginError = null;
        IsLoginOpen = true;
    }

    [RelayCommand]
    private async Task AddMemberAsync()
    {
        if (!RequireAdministrator()) { MemberError = AdministratorRequiredMessage; return; }
        try
        {
            if (SelectedSpace is null) throw new InvalidOperationException("Selecione um espaco financeiro.");
            await _financeStore.AddMemberAsync(new AddMemberRequest(SelectedSpace.Id, NewMemberName, SelectedMemberRole.Role));
            NewMemberName = string.Empty;
            MemberError = null;
            await LoadAsync();
            MemberNotice = "Membro adicionado ao espaco.";
        }
        catch (Exception exception) { MemberError = exception.Message; }
    }

    [RelayCommand]
    private async Task AddFinancialSpaceAsync()
    {
        if (!RequireAdministrator()) { SpaceError = AdministratorRequiredMessage; return; }
        try
        {
            var name = NewSpaceName.Trim();
            await _financeStore.AddFinancialSpaceAsync(name, AuthenticatedUserName);
            NewSpaceName = string.Empty;
            SpaceError = null;
            SpaceNotice = "Espaco criado com sucesso.";
            await LoadAsync();
            SelectedSpace = Spaces.FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception exception) { SpaceError = exception.Message; }
    }

    [RelayCommand]
    private async Task RenameFinancialSpaceAsync()
    {
        if (!RequireAdministrator()) { SpaceError = AdministratorRequiredMessage; return; }
        try
        {
            if (SelectedSpace is null) throw new InvalidOperationException("Selecione um espaco.");
            await _financeStore.RenameFinancialSpaceAsync(SelectedSpace.Id, NewSpaceName);
            NewSpaceName = string.Empty;
            SpaceError = null;
            SpaceNotice = "Nome do espaco atualizado.";
            await LoadAsync();
        }
        catch (Exception exception) { SpaceError = exception.Message; }
    }

    [RelayCommand]
    private void RequestDeleteFinancialSpace()
    {
        if (!RequireAdministrator()) { SpaceError = AdministratorRequiredMessage; return; }
        if (SelectedSpace is null || Spaces.Count <= 1) return;
        CancelDelete();
        _spacePendingDeletion = SelectedSpace;
        DeleteConfirmationText = $"Excluir o espaco '{SelectedSpace.Name}'?";
        DeleteConfirmationHint = "Os participantes deste espaco perderao o acesso. Os dados financeiros permanecerao no aplicativo.";
        IsDeleteConfirmationOpen = true;
    }

    [RelayCommand]
    private void RequestDeleteMember(MemberViewModel member)
    {
        if (!RequireAdministrator()) { MemberError = AdministratorRequiredMessage; return; }
        CancelDelete();
        _memberPendingDeletion = member;
        DeleteConfirmationText = $"Remover '{member.Name}' deste espaco?";
        DeleteConfirmationHint = "O historico financeiro sera preservado, mas o participante perdera o acesso ao espaco.";
        IsDeleteConfirmationOpen = true;
    }

    [RelayCommand]
    private async Task ToggleMemberRoleAsync(MemberViewModel member)
    {
        if (!RequireAdministrator()) { MemberError = AdministratorRequiredMessage; return; }
        try
        {
            var role = member.Role == FinancialSpaceRole.Administrator
                ? FinancialSpaceRole.Member
                : FinancialSpaceRole.Administrator;
            await _financeStore.UpdateMemberRoleAsync(new UpdateMemberRoleRequest(member.Id, role));
            MemberError = null;
            await LoadAsync();
            MemberNotice = role == FinancialSpaceRole.Administrator
                ? $"{member.Name} agora e administrador."
                : $"{member.Name} agora e membro.";
        }
        catch (Exception exception) { MemberError = exception.Message; }
    }

    [RelayCommand]
    private async Task SaveSalaryAsync()
    {
        try
        {
            if (SelectedActiveMember is null) throw new InvalidOperationException("Entre com seu usuario para configurar o salario.");
            if (SalaryAmount is not > 0) throw new InvalidOperationException("Informe o valor do salario.");
            if (SalaryPayDay is null || SalaryPayDay != decimal.Truncate(SalaryPayDay.Value) || SalaryPayDay is < 1 or > 31)
                throw new InvalidOperationException("Informe um dia inteiro entre 1 e 31.");
            if (SelectedSalaryAccount is null) throw new InvalidOperationException("Selecione a conta que recebe o salario.");
            await _financeStore.SaveSalarySettingsAsync(new SaveSalarySettingsRequest(
                SelectedActiveMember.Id, SalaryAmount.Value, (int)SalaryPayDay.Value, SelectedSalaryAccount.Id));
            SalaryError = null;
            await LoadAsync();
            SalaryNotice = "Salario configurado. O lancamento sera feito automaticamente no dia informado.";
        }
        catch (Exception exception) { SalaryError = exception.Message; SalaryNotice = null; }
    }

    [RelayCommand]
    private async Task AddReserveAsync()
    {
        try
        {
            if (_editingReserveId is Guid id)
                await _financeStore.UpdateReserveAsync(new UpdateReserveRequest(id, NewReserveName));
            else
                await _financeStore.AddReserveAsync(new CreateReserveRequest(NewReserveName));
            NewReserveName = string.Empty;
            _editingReserveId = null;
            IsEditingReserve = false;
            ReserveFormTitle = "Nova reserva";
            ReserveSaveButtonText = "Criar reserva";
            ReserveError = null;
            await LoadAsync();
            ReserveNotice = "Reserva criada com sucesso.";
        }
        catch (Exception exception) { ReserveError = exception.Message; }
    }

    [RelayCommand]
    private void EditReserve(ReserveViewModel reserve)
    {
        _editingReserveId = reserve.Id;
        IsEditingReserve = true;
        NewReserveName = reserve.Name;
        ReserveFormTitle = "Editar reserva";
        ReserveSaveButtonText = "Salvar alteracoes";
    }

    [RelayCommand]
    private void CancelReserveEdit()
    {
        _editingReserveId = null; NewReserveName = string.Empty;
        IsEditingReserve = false;
        ReserveFormTitle = "Nova reserva"; ReserveSaveButtonText = "Criar reserva";
    }

    [RelayCommand]
    private async Task SaveReserveMovementAsync()
    {
        if (SelectedReserve is null || SelectedReserveAccount is null || ReserveMovementAmount is not decimal amount)
        {
            ReserveError = "Selecione a reserva, a conta e informe o valor.";
            return;
        }
        try
        {
            await _financeStore.AddReserveMovementAsync(new AddReserveMovementRequest(
                SelectedReserve.Id, SelectedReserveAccount.Id, amount, IsReserveDeposit,
                DateOnly.FromDateTime(DateTime.Today)));
            ReserveMovementAmount = null;
            ReserveError = null;
            await LoadAsync();
            ReserveNotice = IsReserveDeposit ? "Aporte registrado." : "Retirada registrada.";
        }
        catch (Exception exception) { ReserveError = exception.Message; }
    }

    [RelayCommand]
    private async Task TransferReserveAsync()
    {
        if (SelectedTransferSourceReserve is null || SelectedTransferDestinationReserve is null ||
            SelectedReserveAccount is null || ReserveTransferAmount is not decimal amount)
        {
            ReserveError = "Selecione origem, destino, conta e valor.";
            return;
        }
        try
        {
            await _financeStore.TransferReserveAsync(new TransferReserveRequest(
                SelectedTransferSourceReserve.Id, SelectedTransferDestinationReserve.Id,
                SelectedReserveAccount.Id, amount, DateOnly.FromDateTime(DateTime.Today)));
            ReserveTransferAmount = null;
            ReserveError = null;
            await LoadAsync();
            ReserveNotice = "Transferencia entre reservas concluida.";
        }
        catch (Exception exception) { ReserveError = exception.Message; }
    }

    partial void OnSelectedTransferSourceReserveChanged(ReserveViewModel? value)
    {
        if (SelectedTransferDestinationReserve?.Id == value?.Id)
            SelectedTransferDestinationReserve = Reserves.FirstOrDefault(item => item.Id != value?.Id);
    }

    [RelayCommand]
    private async Task AddGoalAsync()
    {
        if (NewGoalTargetAmount is not decimal target)
        {
            GoalError = "Informe o nome e o valor alvo.";
            return;
        }
        try
        {
            var monthlyContribution = NewGoalMonthlyContribution ?? 0m;
            if (_editingGoalId is Guid id)
                await _financeStore.UpdateGoalAsync(new UpdateGoalRequest(
                    id, NewGoalName, target, null, monthlyContribution, SelectedGoalReserve?.Id));
            else
                await _financeStore.AddGoalAsync(new CreateGoalRequest(
                    NewGoalName, target, null, monthlyContribution, SelectedGoalReserve?.Id));
            NewGoalName = string.Empty;
            NewGoalTargetAmount = null;
            NewGoalMonthlyContribution = 0m;
            _editingGoalId = null;
            IsEditingGoal = false;
            GoalFormTitle = "Novo objetivo";
            GoalSaveButtonText = "Criar objetivo";
            GoalError = null;
            await LoadAsync();
            GoalNotice = "Objetivo criado com sucesso.";
        }
        catch (Exception exception) { GoalError = exception.Message; }
    }

    [RelayCommand]
    private void EditGoal(GoalViewModel goal)
    {
        _editingGoalId = goal.Id; NewGoalName = goal.Name; NewGoalTargetAmount = goal.TargetAmount;
        IsEditingGoal = true;
        NewGoalMonthlyContribution = goal.PlannedMonthlyContribution;
        SelectedGoalReserve = Reserves.FirstOrDefault(item => item.Id == goal.ReserveId);
        GoalFormTitle = "Editar objetivo"; GoalSaveButtonText = "Salvar alteracoes";
    }

    [RelayCommand]
    private void CancelGoalEdit()
    {
        _editingGoalId = null; NewGoalName = string.Empty; NewGoalTargetAmount = null;
        IsEditingGoal = false;
        NewGoalMonthlyContribution = 0m;
        GoalFormTitle = "Novo objetivo"; GoalSaveButtonText = "Criar objetivo";
    }

    private GoalViewModel CreateGoalViewModel(GoalItem goal)
    {
        var percentage = goal.TargetAmount <= 0m ? 0d : Math.Min(100d, (double)(goal.CurrentAmount / goal.TargetAmount * 100m));
        var remaining = Math.Max(0m, goal.TargetAmount - goal.CurrentAmount);
        string forecast;
        int? forecastMonths = null;
        if (remaining == 0m) forecast = "Objetivo concluido";
        else if (goal.PlannedMonthlyContribution > 0m || goal.AverageMonthlyContribution > 0m)
        {
            var monthlyContribution = goal.PlannedMonthlyContribution > 0m
                ? goal.PlannedMonthlyContribution
                : goal.AverageMonthlyContribution;
            var months = (int)Math.Ceiling(remaining / monthlyContribution);
            forecastMonths = months;
            var source = goal.PlannedMonthlyContribution > 0m ? "aporte planejado" : "media real dos aportes";
            forecast = months == 1 ? $"Previsao: 1 mes ({source})" : $"Previsao: {months} meses ({source})";
        }
        else forecast = "Defina um aporte mensal para obter previsao";
        return new GoalViewModel(goal.Id, goal.Name, goal.TargetAmount,
            goal.PlannedMonthlyContribution, goal.ReserveId,
            goal.CurrentAmount.ToString("C", BrazilianCulture), goal.TargetAmount.ToString("C", BrazilianCulture),
            $"{goal.CurrentAmount.ToString("C", BrazilianCulture)} de {goal.TargetAmount.ToString("C", BrazilianCulture)}",
            percentage, $"{Math.Round(percentage):N0}%", forecast, forecastMonths, remaining == 0m, goal.IsPrimary,
            goal.PlannedMonthlyContribution > 0m
                ? $"Aporte desejado: {goal.PlannedMonthlyContribution.ToString("C", BrazilianCulture)} por mes"
                : "Aporte desejado: nao informado", this);
    }

    [RelayCommand]
    private void OpenPayableForm()
    {
        PayableDescription = string.Empty;
        PayableAmount = null;
        PayableDueDate = DateTime.Today;
        SelectedPayableAccount = Accounts.FirstOrDefault();
        SelectedPayableCategory = ExpenseCategoryOptions.FirstOrDefault();
        IsRecurringPayable = false;
        IsInstallmentPayable = false;
        PayableInstallmentCount = 2m;
        PayableInstallmentStart = 1m;
        PayableAmountLabel = "Valor";
        PayableError = null;
        IsPayableFormOpen = true;
    }

    [RelayCommand]
    private void ClosePayableForm()
    {
        IsPayableFormOpen = false;
        PayableError = null;
    }

    [RelayCommand]
    private async Task SavePayableAsync()
    {
        if (string.IsNullOrWhiteSpace(PayableDescription) ||
            PayableAmount is not decimal amount || amount <= 0m || amount > 1_000_000_000m ||
            PayableDueDate is null || SelectedPayableAccount is null || SelectedPayableCategory is null)
        {
            PayableError = "Preencha descricao, valor, vencimento, conta e categoria.";
            return;
        }
        try
        {
            var dueDate = DateOnly.FromDateTime(PayableDueDate.Value);
            if (IsInstallmentPayable &&
                (PayableInstallmentCount is not decimal payableCount || payableCount < 2m || payableCount > 60m ||
                 payableCount != decimal.Truncate(payableCount) ||
                 PayableInstallmentStart is not decimal payableStart || payableStart < 1m || payableStart > payableCount ||
                 payableStart != decimal.Truncate(payableStart)))
            {
                PayableError = "Informe o total e a primeira parcela em aberto entre 1 e 60.";
                return;
            }
            if (IsRecurringPayable && IsInstallmentPayable)
            {
                PayableError = "Escolha recorrencia ou parcelamento.";
                return;
            }
            if (IsRecurringPayable)
            {
                await _financeStore.AddRecurringPayableAsync(new CreateRecurringPayableRequest(
                    PayableDescription, amount, dueDate.Day, dueDate, null,
                    SelectedPayableAccount.Id, SelectedPayableCategory.Id));
            }
            else
            {
                await _financeStore.AddPayableAsync(new CreatePayableRequest(
                    PayableDescription, amount, dueDate, SelectedPayableAccount.Id, SelectedPayableCategory.Id,
                    IsInstallmentPayable ? decimal.ToInt32(PayableInstallmentCount!.Value) : 1,
                    IsInstallmentPayable ? decimal.ToInt32(PayableInstallmentStart!.Value) : 1));
            }
            IsPayableFormOpen = false;
            await LoadAsync();
            PayableNotice = IsRecurringPayable ? "Conta recorrente cadastrada." :
                IsInstallmentPayable ? "Parcelas pendentes cadastradas." : "Pendencia cadastrada.";
        }
        catch (Exception exception)
        {
            PayableError = exception.Message;
        }
    }

    [RelayCommand]
    private async Task PayPayableAsync(PayableViewModel payable)
    {
        try
        {
            await _financeStore.PayPayableAsync(new PayPayableRequest(
                payable.Id, DateOnly.FromDateTime(DateTime.Today), ActiveUserId, ActiveUserName));
            await LoadAsync();
            PayableNotice = "Pagamento registrado com sucesso.";
            PayableError = null;
        }
        catch (Exception exception)
        {
            PayableError = exception.Message;
        }
    }

    [RelayCommand]
    private void RequestDeletePayable(PayableViewModel payable)
    {
        CancelDelete();
        _payablePendingDeletion = payable;
        DeleteConfirmationText = $"Excluir a pendencia '{payable.Description}'?";
        DeleteConfirmationHint = payable.IsRecurring
            ? "Somente esta ocorrencia sera excluida; as dos outros meses continuam. Se paga, o pagamento tambem sai dos saldos e relatorios."
            : !payable.CanPay
                ? "O pagamento e o lancamento associado tambem serao removidos dos saldos e relatorios."
                : "Esta acao nao podera ser desfeita.";
        IsDeleteConfirmationOpen = true;
    }

    [RelayCommand]
    private void RequestDeleteRecurringPayable(RecurringPayableViewModel recurring)
    {
        CancelDelete();
        _recurringPayablePendingDeletion = recurring;
        DeleteConfirmationText = $"Encerrar a recorrencia '{recurring.Description}'?";
        DeleteConfirmationHint = "As ocorrencias futuras ainda nao pagas serao removidas. Pagamentos anteriores serao preservados.";
        IsDeleteConfirmationOpen = true;
    }

    [RelayCommand]
    private void RequestDeleteRemainingInstallments(InstallmentPlanViewModel plan)
    {
        if (!plan.IsAccountPayable) return;
        CancelDelete();
        _installmentPlanPendingDeletion = plan;
        DeleteConfirmationText = $"Excluir as parcelas restantes de '{plan.Description}'?";
        DeleteConfirmationHint = "As parcelas ja pagas e seus lancamentos serao preservados.";
        IsDeleteConfirmationOpen = true;
    }

    [RelayCommand]
    private void CloseTransactionForm()
    {
        IsTransactionFormOpen = false;
        FormError = null;
    }

    [RelayCommand]
    private void OpenSettings()
    {
        if (!IsActiveAdministrator) return;
        SettingsError = null;
        SettingsNotice = null;
        SelectedCategoryType = CategoryTypes[1];
        IsSettingsOpen = true;
    }

    [RelayCommand]
    private void CloseSettings()
    {
        IsSettingsOpen = false;
        SettingsError = null;
        SettingsNotice = null;
    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        if (!RequireAdministrator()) { SettingsError = AdministratorRequiredMessage; return; }
        try
        {
            var backup = await _financeStore.CreateBackupAsync();
            SettingsError = null;
            await LoadAsync();
            SettingsNotice = $"Backup criado em {backup.CreatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}.";
        }
        catch (Exception exception) { SettingsError = exception.Message; }
    }

    [RelayCommand]
    private void RequestRestoreLatestBackup()
    {
        if (!RequireAdministrator()) { SettingsError = AdministratorRequiredMessage; return; }
        if (_latestBackupPath is null)
        {
            SettingsError = "Nenhum backup disponivel para restaurar.";
            return;
        }
        IsRestoreConfirmationOpen = true;
    }

    [RelayCommand]
    private void CancelRestoreBackup() => IsRestoreConfirmationOpen = false;

    [RelayCommand]
    private async Task ConfirmRestoreBackupAsync()
    {
        try
        {
            var selectedBackup = _latestBackupPath;
            if (selectedBackup is null) throw new InvalidOperationException("Nenhum backup disponivel para restaurar.");
            await _financeStore.CreateBackupAsync();
            await _financeStore.RestoreBackupAsync(selectedBackup);
            IsRestoreConfirmationOpen = false;
            SettingsError = null;
            await LoadAsync();
            SettingsNotice = "Backup restaurado. Uma copia de seguranca do estado anterior foi criada.";
        }
        catch (Exception exception) { SettingsError = exception.Message; }
    }

    [RelayCommand]
    private void OpenCards()
    {
        if (!IsActiveAdministrator) return;
        CardError = null;
        CardNotice = null;
        SelectedCardPaymentAccount ??= Accounts.FirstOrDefault();
        IsCardsOpen = true;
    }

    [RelayCommand]
    private void CloseCards()
    {
        IsCardsOpen = false;
        CardError = null;
        CardNotice = null;
        ResetCreditCardForm();
    }

    [RelayCommand]
    private void EditCreditCard(CreditCardOption card)
    {
        _editingCreditCardId = card.Id;
        IsEditingCreditCard = true;
        CardFormTitle = "Editar cartao";
        CardSaveButtonText = "Salvar alteracoes";
        NewCardName = card.Name;
        NewCardLimit = card.CreditLimit;
        NewCardClosingDay = card.ClosingDay.ToString(BrazilianCulture);
        NewCardDueDay = card.DueDay.ToString(BrazilianCulture);
        SelectedCardPaymentAccount = Accounts.FirstOrDefault(item => item.Id == card.PaymentAccountId);
        CardError = null;
        CardNotice = null;
    }

    [RelayCommand]
    private void CancelCreditCardEdit() => ResetCreditCardForm();

    [RelayCommand]
    private async Task SaveAccountAsync()
    {
        if (!RequireAdministrator()) { SettingsError = AdministratorRequiredMessage; return; }
        if (NewAccountBalance is < -1_000_000_000m or > 1_000_000_000m)
        {
            SettingsError = "O saldo inicial deve estar entre -R$ 1 bilhao e R$ 1 bilhao.";
            return;
        }

        try
        {
            await _financeStore.AddAccountAsync(new CreateAccountRequest(NewAccountName, SelectedAccountType.Type, NewAccountBalance));
            NewAccountName = string.Empty;
            NewAccountBalance = 0m;
            SettingsError = null;
            SettingsNotice = "Conta adicionada com sucesso.";
            await LoadAsync();
        }
        catch (Exception exception)
        {
            SettingsError = exception.Message;
        }
    }

    [RelayCommand]
    private async Task SaveCategoryAsync()
    {
        if (!RequireAdministrator()) { SettingsError = AdministratorRequiredMessage; return; }
        try
        {
            await _financeStore.AddCategoryAsync(new CreateCategoryRequest(
                NewCategoryName, SelectedCategoryType.Type, SelectedParentCategory?.Id));
            NewCategoryName = string.Empty;
            SelectedParentCategory = null;
            SettingsError = null;
            SettingsNotice = "Categoria adicionada com sucesso.";
            await LoadAsync();
        }
        catch (Exception exception)
        {
            SettingsError = exception.Message;
        }
    }

    [RelayCommand]
    private void RequestDeleteAccount(AccountOption account)
    {
        _accountPendingDeletion = account;
        _categoryPendingDeletion = null;
        _creditCardPendingDeletion = null;
        _payablePendingDeletion = null;
        _transactionPendingDeletion = null;
        _payablePendingDeletion = null;
        DeleteConfirmationText = $"Excluir a conta '{account.Name}'?";
        DeleteConfirmationHint = "Cadastros usados em lancamentos sao protegidos e nao podem ser apagados.";
        IsDeleteConfirmationOpen = true;
    }

    [RelayCommand]
    private void RequestDeleteCategory(CategoryOption category)
    {
        _categoryPendingDeletion = category;
        _accountPendingDeletion = null;
        _creditCardPendingDeletion = null;
        _transactionPendingDeletion = null;
        _payablePendingDeletion = null;
        DeleteConfirmationText = $"Excluir a categoria '{category.Name}'?";
        DeleteConfirmationHint = "Cadastros usados em lancamentos sao protegidos e nao podem ser apagados.";
        IsDeleteConfirmationOpen = true;
    }

    [RelayCommand]
    private void CancelDelete()
    {
        IsDeleteConfirmationOpen = false;
        _accountPendingDeletion = null;
        _categoryPendingDeletion = null;
        _creditCardPendingDeletion = null;
        _transactionPendingDeletion = null;
        _payablePendingDeletion = null;
        _recurringPayablePendingDeletion = null;
        _installmentPlanPendingDeletion = null;
        _reservePendingDeletion = null;
        _goalPendingDeletion = null;
        _memberPendingDeletion = null;
        _spacePendingDeletion = null;
    }

    [RelayCommand]
    private void RequestDeleteReserve(ReserveViewModel reserve)
    {
        CancelDelete(); _reservePendingDeletion = reserve;
        DeleteConfirmationText = $"Excluir a reserva '{reserve.Name}'?";
        DeleteConfirmationHint = "Reservas com movimentacoes ou objetivos vinculados sao protegidas.";
        IsDeleteConfirmationOpen = true;
    }

    [RelayCommand]
    private void RequestDeleteGoal(GoalViewModel goal)
    {
        CancelDelete(); _goalPendingDeletion = goal;
        DeleteConfirmationText = $"Excluir o objetivo '{goal.Name}'?";
        DeleteConfirmationHint = "A reserva vinculada e suas movimentacoes serao preservadas.";
        IsDeleteConfirmationOpen = true;
    }

    [RelayCommand]
    private async Task SetPrimaryGoalAsync(GoalViewModel goal)
    {
        try
        {
            await _financeStore.SetPrimaryGoalAsync(goal.Id);
            await LoadAsync();
            GoalError = null;
            GoalNotice = $"'{goal.Name}' agora aparece na Visao geral.";
        }
        catch (Exception exception) { GoalError = exception.Message; }
    }

    [RelayCommand]
    private void RequestDeleteCreditCard(CreditCardOption card)
    {
        _creditCardPendingDeletion = card;
        _accountPendingDeletion = null;
        _categoryPendingDeletion = null;
        _transactionPendingDeletion = null;
        _payablePendingDeletion = null;
        DeleteConfirmationText = $"Excluir o cartao '{card.Name}'?";
        DeleteConfirmationHint = "Esta acao nao podera ser desfeita.";
        IsDeleteConfirmationOpen = true;
    }

    [RelayCommand]
    private void RequestDeleteTransaction(LedgerTransactionViewModel transaction)
    {
        LedgerNotice = null;
        LedgerError = null;
        _transactionPendingDeletion = transaction;
        _accountPendingDeletion = null;
        _categoryPendingDeletion = null;
        _creditCardPendingDeletion = null;
        _payablePendingDeletion = null;
        DeleteConfirmationText = $"Excluir o lancamento '{transaction.Description}'?";
        DeleteConfirmationHint = "O lancamento deixara de participar dos saldos e relatorios.";
        IsDeleteConfirmationOpen = true;
    }

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        var successMessage = string.Empty;
        var deletingSettingsItem = _accountPendingDeletion is not null || _categoryPendingDeletion is not null;
        var deletingPayable = _payablePendingDeletion is not null ||
            _recurringPayablePendingDeletion is not null || _installmentPlanPendingDeletion is not null;
        try
        {
            if (_accountPendingDeletion is not null)
            {
                await _financeStore.DeleteAccountAsync(_accountPendingDeletion.Id);
                successMessage = "Conta excluida com sucesso.";
            }
            else if (_categoryPendingDeletion is not null)
            {
                await _financeStore.DeleteCategoryAsync(_categoryPendingDeletion.Id);
                successMessage = "Categoria excluida com sucesso.";
            }
            else if (_creditCardPendingDeletion is not null)
            {
                await _financeStore.DeleteCreditCardAsync(_creditCardPendingDeletion.Id);
                successMessage = "Cartao excluido com sucesso.";
            }
            else if (_transactionPendingDeletion is not null)
            {
                await _financeStore.DeleteTransactionAsync(_transactionPendingDeletion.Id);
                successMessage = "Lancamento excluido com sucesso.";
            }
            else if (_payablePendingDeletion is not null)
            {
                await _financeStore.DeletePayableAsync(_payablePendingDeletion.Id);
                successMessage = "Pendencia excluida com sucesso.";
            }
            else if (_recurringPayablePendingDeletion is not null)
            {
                await _financeStore.DeleteRecurringPayableAsync(_recurringPayablePendingDeletion.Id);
                successMessage = "Recorrencia encerrada com sucesso.";
            }
            else if (_installmentPlanPendingDeletion is not null)
            {
                await _financeStore.DeleteRemainingInstallmentsAsync(_installmentPlanPendingDeletion.GroupId);
                successMessage = "Parcelas restantes excluidas com sucesso.";
            }
            else if (_reservePendingDeletion is not null)
            {
                await _financeStore.DeleteReserveAsync(_reservePendingDeletion.Id);
                successMessage = "Reserva excluida com sucesso.";
            }
            else if (_goalPendingDeletion is not null)
            {
                await _financeStore.DeleteGoalAsync(_goalPendingDeletion.Id);
                successMessage = "Objetivo excluido com sucesso.";
            }
            else if (_memberPendingDeletion is not null)
            {
                if (!RequireAdministrator()) throw new InvalidOperationException(AdministratorRequiredMessage);
                await _financeStore.DeleteMemberAsync(_memberPendingDeletion.Id);
                successMessage = "Membro removido do espaco.";
            }
            else if (_spacePendingDeletion is not null)
            {
                if (!RequireAdministrator()) throw new InvalidOperationException(AdministratorRequiredMessage);
                var replacement = Spaces.FirstOrDefault(item => item.Id != _spacePendingDeletion.Id);
                await _financeStore.DeleteFinancialSpaceAsync(_spacePendingDeletion.Id, replacement?.Id);
                SelectedSpace = null;
                successMessage = "Espaco excluido.";
            }

            SettingsError = null;
            CardError = null;
            PayableError = null;
            CancelDelete();
            await LoadAsync();
            if (deletingSettingsItem)
            {
                SettingsNotice = successMessage;
            }
            else if (deletingPayable || IsPayablesPage)
            {
                PayableNotice = successMessage;
            }
            else if (IsTransactionsPage)
            {
                LedgerNotice = successMessage;
            }
            else if (IsCardsOpen)
            {
                CardNotice = successMessage;
            }
            else if (IsMembersPage)
            {
                if (successMessage == "Espaco excluido.") SpaceNotice = successMessage;
                else MemberNotice = successMessage;
            }
            else
            {
                SettingsNotice = successMessage;
            }
        }
        catch (Exception exception)
        {
            if (deletingSettingsItem)
            {
                SettingsError = exception.Message;
            }
            else if (deletingPayable || IsPayablesPage)
            {
                PayableError = exception.Message;
            }
            else if (IsTransactionsPage)
            {
                LedgerError = exception.Message;
            }
            else if (IsCardsOpen)
            {
                CardError = exception.Message;
            }
            else if (IsMembersPage)
            {
                if (_spacePendingDeletion is not null) SpaceError = exception.Message;
                else MemberError = exception.Message;
            }
            else
            {
                SettingsError = exception.Message;
            }
            IsDeleteConfirmationOpen = false;
        }
    }

    [RelayCommand]
    private async Task SaveCreditCardAsync()
    {
        CardError = ValidateCreditCard();
        if (CardError is not null)
        {
            return;
        }

        try
        {
            if (_editingCreditCardId is Guid cardId)
            {
                await _financeStore.UpdateCreditCardAsync(new UpdateCreditCardRequest(
                    cardId, NewCardName, SelectedCardPaymentAccount!.Id, NewCardLimit!.Value,
                    int.Parse(NewCardClosingDay, BrazilianCulture),
                    int.Parse(NewCardDueDay, BrazilianCulture)));
            }
            else
            {
                await _financeStore.AddCreditCardAsync(new CreateCreditCardRequest(
                    NewCardName, SelectedCardPaymentAccount!.Id, NewCardLimit!.Value,
                    int.Parse(NewCardClosingDay, BrazilianCulture),
                    int.Parse(NewCardDueDay, BrazilianCulture)));
            }

            var successMessage = _editingCreditCardId is null
                ? "Cartao adicionado com sucesso."
                : "Cartao atualizado com sucesso.";
            ResetCreditCardForm();
            CardError = null;
            await LoadAsync();
            CardNotice = successMessage;
        }
        catch (Exception exception)
        {
            CardError = exception.Message;
        }
    }

    internal async Task PersistCreditCardLimitAsync(CreditCardLimitViewModel card, CancellationToken cancellationToken)
    {
        try
        {
            await _financeStore.UpdateCreditCardLimitAsync(
                new UpdateCreditCardLimitRequest(card.Id, card.CreditLimit),
                cancellationToken);
            CardError = null;
            CardNotice = $"Limite do {card.Name} atualizado com sucesso.";
        }
        catch (OperationCanceledException)
        {
            // Outro movimento do controle substituiu este salvamento.
        }
        catch (Exception exception)
        {
            CardError = exception.Message;
        }
    }

    [RelayCommand]
    private async Task SaveTransactionAsync()
    {
        FormError = ValidateForm();
        if (FormError is not null)
        {
            return;
        }

        IsSaving = true;
        try
        {
            var amount = TransactionAmount!.Value;
            var transactionDate = DateOnly.FromDateTime(TransactionDate!.Value);
            var categoryId = SelectedCategory?.Id ?? Guid.Empty;
            var creditCardId = IsCardPurchase ? SelectedPurchaseCard?.Id : null;
            if (_editingTransactionId is Guid transactionId)
            {
                await _financeStore.UpdateTransactionAsync(new UpdateTransactionRequest(
                    transactionId,
                    SelectedAccount!.Id,
                    categoryId,
                    SelectedTransactionType.Type,
                    TransactionDescription,
                    amount,
                    transactionDate,
                    IsTransferSelected ? SelectedDestinationAccount?.Id : null,
                    creditCardId));
            }
            else
            {
                await _financeStore.AddTransactionAsync(new CreateTransactionRequest(
                    SelectedAccount!.Id,
                    categoryId,
                    SelectedTransactionType.Type,
                    TransactionDescription,
                    amount,
                    transactionDate,
                    ActiveUserId,
                    ActiveUserName,
                    IsTransferSelected ? SelectedDestinationAccount?.Id : null,
                    creditCardId,
                    IsExpenseSelected && IsInstallmentPurchase ? decimal.ToInt32(InstallmentCount!.Value) : 1,
                    IsExpenseSelected && IsInstallmentPurchase ? decimal.ToInt32(InstallmentStart!.Value) : 1));
            }

            IsTransactionFormOpen = false;
            _editingTransactionId = null;
            ResetReportDrilldown();
            SelectPeriod(transactionDate);
            await LoadAsync();
            LedgerSaveNotice = $"Lancamento salvo em {transactionDate:dd/MM/yyyy}.";
        }
        catch (Exception exception)
        {
            FormError = $"Nao foi possivel salvar: {exception.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    partial void OnSelectedTransactionTypeChanged(TransactionTypeOption value)
    {
        IsTransferSelected = value.Type == TransactionType.Transfer;
        IsExpenseSelected = value.Type == TransactionType.Expense;
        UpdateCardPurchaseState();
        RefreshCategories();
        if (IsTransferSelected)
        {
            SelectedDestinationAccount = Accounts.FirstOrDefault(item => item.Id != SelectedAccount?.Id);
        }
    }

    partial void OnSelectedPaymentMethodChanged(PaymentMethodOption value) => UpdateCardPurchaseState();

    partial void OnIsRecurringPayableChanged(bool value)
    {
        if (value) IsInstallmentPayable = false;
    }

    partial void OnIsInstallmentPayableChanged(bool value)
    {
        if (value) IsRecurringPayable = false;
        PayableAmountLabel = value ? "Valor de cada parcela" : "Valor";
    }

    partial void OnSelectedActiveMemberChanged(MemberViewModel? value)
    {
        IsActiveAdministrator = value?.Role == FinancialSpaceRole.Administrator;
        ActivePermissionText = IsActiveAdministrator ? "Acesso administrativo" : "Acesso de membro";
    }

    partial void OnSelectedLoginUserChanged(LoginUserOption? value)
    {
        IsCreatingPin = value is { HasPin: false };
        LoginTitle = IsCreatingPin ? "Crie seu PIN de acesso" : "Entrar no Financeiro";
        LoginActionText = IsCreatingPin ? "Criar PIN e entrar" : "Entrar";
        LoginPin = string.Empty;
        LoginPinConfirmation = string.Empty;
        LoginError = null;
    }

    partial void OnSelectedSpaceChanged(FinancialSpaceOption? value)
    {
        if (SelectedSpaceId != value?.Id)
            SelectedSpaceId = value?.Id;
        CanDeleteSelectedSpace = value is not null && Spaces.Count > 1;
        if (_canReloadSpace) _ = LoadAsync();
    }

    partial void OnSelectedSpaceIdChanged(Guid? value)
    {
        var space = Spaces.FirstOrDefault(item => item.Id == value);
        if (!Equals(space, SelectedSpace))
            SelectedSpace = space;
    }

    partial void OnSelectedCategoryTypeChanged(TransactionTypeOption value) => RefreshParentCategories();

    partial void OnSelectedAccountChanged(AccountOption? value)
    {
        if (IsTransferSelected && SelectedDestinationAccount?.Id == value?.Id)
        {
            SelectedDestinationAccount = Accounts.FirstOrDefault(item => item.Id != value?.Id);
        }
    }

    partial void OnLedgerSearchTextChanged(string value) => ApplyTransactionFilters();

    partial void OnSelectedLedgerTypeChanged(TransactionTypeFilterOption value) => ApplyTransactionFilters();

    partial void OnSelectedLedgerAccountChanged(AccountFilterOption value) => ApplyTransactionFilters();

    partial void OnSelectedLedgerCategoryChanged(CategoryFilterOption value) => ApplyTransactionFilters();

    partial void OnSelectedPeriodChanged(string value)
    {
        if (_canReloadPeriod)
        {
            _ = LoadAsync();
        }
    }

    private string? ValidateForm()
    {
        if (string.IsNullOrWhiteSpace(TransactionDescription))
        {
            return "Informe uma descricao.";
        }

        if (TransactionAmount is not decimal amount || amount <= 0 || amount > 1_000_000_000m)
        {
            return "Informe um valor entre R$ 0,01 e R$ 1 bilhao.";
        }

        if (IsExpenseSelected && IsInstallmentPurchase &&
            (InstallmentCount is not decimal count || count < 2m || count > 60m || count != decimal.Truncate(count) ||
             InstallmentStart is not decimal start || start < 1m || start > count ||
             start != decimal.Truncate(start)))
        {
            return "Informe uma quantidade de parcelas entre 1 e 60.";
        }

        if (SelectedAccount is null)
        {
            return "Selecione uma conta.";
        }

        if (IsTransferSelected &&
            (SelectedDestinationAccount is null || SelectedDestinationAccount.Id == SelectedAccount.Id))
        {
            return "Selecione contas de origem e destino diferentes.";
        }

        if (IsCardPurchase && SelectedPurchaseCard is null)
        {
            return "Cadastre e selecione um cartao de credito.";
        }

        if (TransactionDate is null)
        {
            return "Informe a data do lancamento.";
        }

        return !IsTransferSelected && SelectedCategory is null ? "Selecione uma categoria." : null;
    }

    private string? ValidateCreditCard()
    {
        if (string.IsNullOrWhiteSpace(NewCardName))
        {
            return "Informe o nome do cartao.";
        }

        if (SelectedCardPaymentAccount is null)
        {
            return "Selecione a conta de pagamento.";
        }

        if (NewCardLimit is not decimal limit || limit <= 0 || limit > 1_000_000_000m)
        {
            return "Informe um limite maior que zero.";
        }

        if (!int.TryParse(NewCardClosingDay, out var closingDay) || closingDay is < 1 or > 31 ||
            !int.TryParse(NewCardDueDay, out var dueDay) || dueDay is < 1 or > 31)
        {
            return "Fechamento e vencimento devem estar entre os dias 1 e 31.";
        }

        return null;
    }

    private void ResetCreditCardForm()
    {
        _editingCreditCardId = null;
        IsEditingCreditCard = false;
        CardFormTitle = "Novo cartao";
        CardSaveButtonText = "Adicionar cartao";
        NewCardName = string.Empty;
        NewCardLimit = null;
        NewCardClosingDay = "10";
        NewCardDueDay = "17";
        SelectedCardPaymentAccount = Accounts.FirstOrDefault();
    }

    private void RefreshCategories()
    {
        if (SelectedTransactionType is null)
        {
            return;
        }

        Categories.Clear();
        foreach (var category in _allCategories.Where(category => category.Type == SelectedTransactionType.Type))
        {
            Categories.Add(new CategoryOption(category.Id,
                category.ParentName is null ? category.Name : $"{category.ParentName} › {category.Name}",
                GetTransactionTypeName(category.Type), this));
        }

        SelectedCategory = Categories.FirstOrDefault();
    }

    private void RefreshParentCategories()
    {
        if (SelectedCategoryType is null) return;
        var selectedId = SelectedParentCategory?.Id;
        ParentCategoryOptions.Clear();
        foreach (var category in _allCategories.Where(item => item.Type == SelectedCategoryType.Type && item.ParentCategoryId is null))
            ParentCategoryOptions.Add(new CategoryOption(category.Id, category.Name, GetTransactionTypeName(category.Type), this));
        SelectedParentCategory = ParentCategoryOptions.FirstOrDefault(item => item.Id == selectedId);
    }

    private void ApplyTransactionFilters()
    {
        var transactions = _allTransactions.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(LedgerSearchText))
        {
            transactions = transactions.Where(item =>
                item.Description.Contains(LedgerSearchText.Trim(), StringComparison.CurrentCultureIgnoreCase));
        }

        if (SelectedLedgerType?.Type is TransactionType type)
        {
            transactions = transactions.Where(item => item.Type == type);
        }

        if (SelectedLedgerAccount?.Id is Guid accountId)
        {
            transactions = transactions.Where(item =>
                item.AccountId == accountId || item.DestinationAccountId == accountId);
        }

        if (SelectedLedgerCategory?.Id is Guid categoryId)
        {
            transactions = transactions.Where(item => item.CategoryId == categoryId);
        }

        if (_reportCardFilter is Guid cardId)
            transactions = transactions.Where(item => item.CreditCardId == cardId);

        if (!string.IsNullOrWhiteSpace(_reportAuthorFilter))
            transactions = transactions.Where(item => item.AuthorName == _reportAuthorFilter);

        FilteredTransactions.Clear();
        foreach (var transaction in transactions)
        {
            var account = Accounts.FirstOrDefault(item => item.Id == transaction.AccountId)?.Name ?? "Conta removida";
            var destination = Accounts.FirstOrDefault(item => item.Id == transaction.DestinationAccountId)?.Name;
            var card = CreditCards.FirstOrDefault(item => item.Id == transaction.CreditCardId)?.Name;
            var category = transaction.Type == TransactionType.Transfer
                ? "Transferencia"
                : AllCategoryOptions.FirstOrDefault(item => item.Id == transaction.CategoryId)?.Name ?? "Sem categoria";
            var isExpense = transaction.Type == TransactionType.Expense;
            var accountText = transaction.Type == TransactionType.Transfer
                ? $"{account} para {destination ?? "Conta removida"}"
                : transaction.CreditCardId is not null ? card ?? "Cartao removido" : account;
            FilteredTransactions.Add(new LedgerTransactionViewModel(
                transaction.Id,
                transaction.AccountId,
                transaction.CategoryId,
                transaction.DestinationAccountId,
                transaction.CreditCardId,
                transaction.Type,
                transaction.Description,
                transaction.Amount,
                transaction.Date,
                transaction.Date.ToString("dd/MM/yyyy", BrazilianCulture),
                GetTransactionTypeName(transaction.Type),
                accountText,
                category,
                transaction.Type == TransactionType.Transfer
                    ? transaction.Amount.ToString("C", BrazilianCulture)
                    : (transaction.Type is TransactionType.Expense or TransactionType.CreditCardPayment ? "- " : "+ ") +
                      transaction.Amount.ToString("C", BrazilianCulture),
                isExpense,
                transaction.Type != TransactionType.CreditCardPayment,
                transaction.AuthorName,
                GetLedgerMetadata(transaction),
                this));
        }

        HasFilteredTransactions = FilteredTransactions.Count > 0;
        IsFilteredTransactionListEmpty = !HasFilteredTransactions;
    }

    private static string GetTransactionTypeName(TransactionType type) => type switch
    {
        TransactionType.Income => "Receita",
        TransactionType.Expense => "Despesa",
        TransactionType.Transfer => "Transferencia",
        TransactionType.CreditCardPayment => "Pagamento de fatura",
        _ => "Lancamento",
    };

    private static string GetCategoryDisplayName(CategoryItem? category) => category is null
        ? "Sem categoria"
        : category.ParentName is null ? category.Name : $"{category.ParentName} › {category.Name}";

    private void UpdateCardPurchaseState() =>
        IsCardPurchase = IsExpenseSelected && SelectedPaymentMethod?.IsCreditCard == true;

    [RelayCommand]
    private async Task PayInvoiceAsync(CreditCardInvoiceViewModel invoice)
    {
        try
        {
            await _financeStore.PayCreditCardInvoiceAsync(new PayCreditCardInvoiceRequest(
                invoice.CreditCardId, invoice.Year, invoice.Month, DateOnly.FromDateTime(DateTime.Today),
                ActiveUserId, ActiveUserName));
            CardError = null;
            await LoadAsync();
            if (IsPayablesPage)
            {
                PayableNotice = "Fatura paga com sucesso.";
            }
            else
            {
                CardNotice = "Fatura paga com sucesso.";
            }
        }
        catch (Exception exception)
        {
            if (IsPayablesPage)
            {
                PayableError = exception.Message;
            }
            else
            {
                CardError = exception.Message;
            }
        }
    }

    private static string GetMonthName(int month) => BrazilianCulture.DateTimeFormat.GetMonthName(month);

    private void SelectPeriod(DateOnly date)
    {
        var label = $"{BrazilianCulture.TextInfo.ToTitleCase(GetMonthName(date.Month))} de {date.Year}";
        if (!_periods.ContainsKey(label))
        {
            _periods[label] = (date.Year, date.Month);
            var index = 0;
            while (index < Periods.Count)
            {
                var period = _periods[Periods[index]];
                if (period.Year < date.Year || period.Year == date.Year && period.Month < date.Month)
                    break;
                index++;
            }
            Periods.Insert(index, label);
        }

        _canReloadPeriod = false;
        SelectedPeriod = label;
        _canReloadPeriod = true;
    }

    private CreditCardInvoiceViewModel CreateInvoiceViewModel(CreditCardInvoiceItem invoice) => new(
        invoice.CreditCardId,
        invoice.CardName,
        $"{BrazilianCulture.TextInfo.ToTitleCase(GetMonthName(invoice.Month))} de {invoice.Year}",
        invoice.Total.ToString("C", BrazilianCulture),
        invoice.Outstanding.ToString("C", BrazilianCulture),
        invoice.UsedLimit.ToString("C", BrazilianCulture),
        invoice.AvailableLimit.ToString("C", BrazilianCulture),
        invoice.ClosingDate.ToString("dd/MM/yyyy", BrazilianCulture),
        invoice.DueDate.ToString("dd/MM/yyyy", BrazilianCulture),
        invoice.Status,
        invoice.Outstanding > 0m,
        invoice.Year,
        invoice.Month,
        this);

    private void InitializePeriods()
    {
        var first = new DateOnly(2026, 8, 1);
        var current = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        if (current < first) current = first;
        var last = current.AddMonths(12);
        for (var date = last; date >= first; date = date.AddMonths(-1))
        {
            var monthName = BrazilianCulture.TextInfo.ToTitleCase(GetMonthName(date.Month));
            var label = $"{monthName} de {date.Year}";
            _periods[label] = (date.Year, date.Month);
            Periods.Add(label);
        }
        SelectedPeriod = $"{BrazilianCulture.TextInfo.ToTitleCase(GetMonthName(current.Month))} de {current.Year}";
    }

    private string GetLedgerMetadata(TransactionItem transaction)
    {
        if (transaction.CreditCardId is not null && transaction.InvoiceYear is int year && transaction.InvoiceMonth is int month)
        {
            return $"Fatura {GetMonthName(month)} de {year} - {transaction.AuthorName}";
        }
        return $"{GetTransactionTypeName(transaction.Type)} - {transaction.AuthorName} - {transaction.DeviceName}";
    }

    private static string GetAccountTypeName(FinancialAccountType type) => type switch
    {
        FinancialAccountType.Checking => "Conta corrente",
        FinancialAccountType.Savings => "Poupanca",
        FinancialAccountType.Wallet => "Carteira",
        FinancialAccountType.Investment => "Investimento",
        _ => "Conta",
    };

    private Guid ActiveUserId => SelectedActiveMember?.Id ?? CurrentUserId;
    private string ActiveUserName => SelectedActiveMember?.Name ?? "Alex";

    private const string AdministratorRequiredMessage = "Somente administradores podem realizar esta operacao.";
    private bool RequireAdministrator() => IsActiveAdministrator;
}

public sealed record AccountOption(
    Guid Id,
    string Name,
    string TypeName,
    string InitialBalance,
    string CurrentBalance,
    MainViewModel Owner)
{
    public override string ToString() => Name;
}

public sealed record ReserveViewModel(Guid Id, string Name, decimal Balance, string BalanceText, MainViewModel Owner)
{
    public override string ToString() => Name;
}

public sealed record ReserveMovementViewModel(
    string ReserveName, string AccountName, string TypeName, string AmountText, string DateText, bool IsDeposit);

public sealed record GoalViewModel(
    Guid Id, string Name, decimal TargetAmount, decimal PlannedMonthlyContribution,
    Guid? ReserveId, string CurrentAmountText, string TargetAmountText,
    string ProgressText, double ProgressPercentage, string ProgressPercentageText,
    string ForecastText, int? ForecastMonths, bool IsCompleted, bool IsPrimary,
    string PlannedContributionText, MainViewModel Owner);

public sealed record MonthlyReportViewModel(
    string Month, string IncomeText, string ExpenseText, double IncomeHeight, double ExpenseHeight);

public sealed record CategoryReportViewModel(
    Guid CategoryId, string Name, string AmountText, double Percentage, string PercentageText);

public enum ReportDrilldownKind { Account, Card, Author }

public sealed record ReportBreakdownViewModel(
    ReportDrilldownKind Kind, Guid? Id, string? Value,
    string Name, string AmountText, string DetailText);

public sealed record MemberRoleOption(FinancialSpaceRole Role, string Name)
{
    public override string ToString() => Name;
}

public sealed record FinancialSpaceOption(Guid Id, string Name, bool IsPrimary)
{
    public override string ToString() => Name;
}

public sealed record LoginUserOption(string Name, bool HasPin)
{
    public override string ToString() => Name;
}

public sealed record MemberViewModel(
    Guid Id, string Name, FinancialSpaceRole Role, string RoleName, bool CanRemove, MainViewModel Owner)
{
    public bool CanChangeRole => CanRemove;
    public bool IsAdministrator => Role == FinancialSpaceRole.Administrator;
    public bool IsMember => !IsAdministrator;
    public string Initials => string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Take(2).Select(part => char.ToUpperInvariant(part[0])));
    public string ChangeRoleText => Role == FinancialSpaceRole.Administrator ? "Tornar membro" : "Tornar administrador";
    public override string ToString() => Name;
}

public sealed record CategoryOption(Guid Id, string Name, string TypeName, MainViewModel Owner)
{
    public override string ToString() => Name;
}

public sealed record CreditCardOption(
    Guid Id,
    string Name,
    Guid PaymentAccountId,
    string AccountName,
    decimal CreditLimit,
    string LimitText,
    int ClosingDay,
    int DueDay,
    MainViewModel Owner)
{
    public override string ToString() => Name;
}

public partial class CreditCardLimitViewModel : ObservableObject
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pt-BR");

    private CancellationTokenSource? _saveCancellation;
    private bool _isReady;
    private bool _isAdjusting;

    public CreditCardLimitViewModel(
        Guid id,
        string name,
        decimal creditLimit,
        decimal maximumLimit,
        decimal usedLimit,
        MainViewModel owner)
    {
        Id = id;
        Name = name;
        UsedLimit = usedLimit;
        MaximumLimit = maximumLimit;
        CreditLimit = creditLimit;
        Owner = owner;
        _isReady = true;
    }

    public Guid Id { get; }
    public string Name { get; }
    public decimal UsedLimit { get; }
    public decimal MaximumLimit { get; }
    public MainViewModel Owner { get; }
    [ObservableProperty] public partial decimal CreditLimit { get; set; }
    public bool IsAdjusting
    {
        get => _isAdjusting;
        private set
        {
            if (SetProperty(ref _isAdjusting, value))
                OnPropertyChanged(nameof(AdjustmentButtonText));
        }
    }
    public string AdjustmentButtonText => IsAdjusting ? "Concluir ajuste" : "Ajustar limite";

    public string UsedLimitText => UsedLimit.ToString("C", Culture);
    public string AvailableLimitText => Math.Max(0m, CreditLimit - UsedLimit).ToString("C", Culture);
    public string CreditLimitText => CreditLimit.ToString("C", Culture);
    public string MaximumLimitText => MaximumLimit.ToString("C", Culture);
    public string UsageText => $"{Math.Round(UsagePercentage):N0}% utilizado";
    public double UsagePercentage => CreditLimit <= 0m
        ? 0d
        : Math.Min(100d, (double)(UsedLimit / CreditLimit * 100m));

    [RelayCommand]
    private void ToggleLimitAdjustment() => IsAdjusting = !IsAdjusting;

    partial void OnCreditLimitChanged(decimal value)
    {
        OnPropertyChanged(nameof(AvailableLimitText));
        OnPropertyChanged(nameof(CreditLimitText));
        OnPropertyChanged(nameof(UsageText));
        OnPropertyChanged(nameof(UsagePercentage));
        if (!_isReady)
        {
            return;
        }

        _saveCancellation?.Cancel();
        _saveCancellation?.Dispose();
        _saveCancellation = new CancellationTokenSource();
        _ = SaveAfterDragAsync(_saveCancellation.Token);
    }

    private async Task SaveAfterDragAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(450, cancellationToken);
            await Owner.PersistCreditCardLimitAsync(this, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // O usuario continuou arrastando; somente o ultimo valor sera salvo.
        }
    }
}

public sealed record PaymentMethodOption(bool IsCreditCard, string Name)
{
    public override string ToString() => Name;
}

public sealed record CreditCardInvoiceViewModel(
    Guid CreditCardId,
    string CardName,
    string Reference,
    string Total,
    string Outstanding,
    string UsedLimit,
    string AvailableLimit,
    string ClosingDate,
    string DueDate,
    string Status,
    bool CanPay,
    int Year,
    int Month,
    MainViewModel Owner);

public sealed record PayableViewModel(
    Guid Id,
    string Description,
    string AmountText,
    decimal RawAmount,
    string DueDateText,
    string Status,
    bool IsRecurring,
    bool CanPay,
    string AccountName,
    string CategoryName,
    MainViewModel Owner);

public sealed record RecurringPayableViewModel(
    Guid Id,
    string Description,
    string AmountText,
    string ScheduleText,
    string AccountName,
    MainViewModel Owner);

public sealed record InstallmentPlanViewModel(
    Guid GroupId, string Description, string PaymentMethod, string ProgressText,
    string NextDueDateText, string NextAmountText, bool IsAccountPayable, MainViewModel Owner);

public sealed record TransactionTypeOption(TransactionType Type, string Name)
{
    public override string ToString() => Name;
}

public sealed record AccountTypeOption(FinancialAccountType Type, string Name)
{
    public override string ToString() => Name;
}

public sealed record RecentTransactionViewModel(
    string Name, string Description, string Amount, string Metadata, bool IsPositive, bool IsNegative);

public sealed record CalendarDayViewModel(
    DateOnly? Date, string DayNumber, bool IsToday, bool HasEvents, string EventCountText, string EventPreviewText)
{
    public bool IsEnabled => Date is not null;
}

public sealed record CalendarEventViewModel(
    string Description, string Kind, string Details, string AmountText, string StatusText, bool IsIncome, bool IsPending);

public sealed record FutureProjectionViewModel(
    string MonthText, string OpeningBalanceText, string IncomeText, string ExpenseText,
    string CommitmentsText, string GoalContributionText, string EndingBalanceText,
    string StatusText, bool IsRisk);

public sealed record TransactionTypeFilterOption(TransactionType? Type, string Name)
{
    public override string ToString() => Name;
}

public sealed record AccountFilterOption(Guid? Id, string Name)
{
    public override string ToString() => Name;
}

public sealed record CategoryFilterOption(Guid? Id, string Name)
{
    public override string ToString() => Name;
}

public sealed record LedgerTransactionViewModel(
    Guid Id,
    Guid AccountId,
    Guid CategoryId,
    Guid? DestinationAccountId,
    Guid? CreditCardId,
    TransactionType Type,
    string Description,
    decimal RawAmount,
    DateOnly Date,
    string DateText,
    string TypeName,
    string AccountName,
    string CategoryName,
    string AmountText,
    bool IsExpense,
    bool CanEdit,
    string AuthorName,
    string Metadata,
    MainViewModel Owner)
{
    public bool IsPositive => Type == TransactionType.Income;
    public bool IsNegative => Type is TransactionType.Expense or TransactionType.CreditCardPayment;
}
