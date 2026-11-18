using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using NickFinancas.Application.Finance;
using NickFinancas.Domain.Accounts;
using NickFinancas.Domain.Transactions;
using NickFinancas.Domain.Spaces;

namespace NickFinancas.Infrastructure.Persistence;

public sealed partial class SqliteFinanceStore : ILocalFinanceStore
{
    private static readonly CultureInfo BrazilianCulture = CultureInfo.GetCultureInfo("pt-BR");
    public static event EventHandler? LocalDataChanged;

    internal static void NotifyLocalDataChanged() => LocalDataChanged?.Invoke(null, EventArgs.Empty);

    private const decimal MaxMonetaryAmount = 1_000_000_000m;
    private static readonly Guid JointAccountId = Guid.Parse("89bdb510-a2ca-4bee-b9b5-5e7347d0eed8");
    private static readonly Guid WalletAccountId = Guid.Parse("c038822a-9406-436d-b4ca-b276309a9401");
    private static readonly Guid FoodCategoryId = Guid.Parse("1143dd3d-5d11-4e89-af03-fdb7ec502bf2");
    private static readonly Guid HomeCategoryId = Guid.Parse("7d66ed54-9e0f-4708-8fb3-c934f5af5508");
    private static readonly Guid SalaryCategoryId = Guid.Parse("770af69d-4eae-4a22-9cc8-f8e0589a717f");
    private static readonly Guid TransferCategoryId = Guid.Parse("259d6e56-50af-43ee-8c3c-ea97f3925507");
    private static readonly Guid PrimaryMemberId = Guid.Parse("619fdf3d-8145-49e3-b1cc-4b6d32b770dd");
    private static readonly Guid PrimaryFinancialSpaceId = Guid.Parse("1a6580fa-9309-4f4d-a7a4-082cc757252b");

    private readonly DbContextOptions<LocalFinanceDbContext> _options;
    private readonly string _databasePath;
    private readonly bool _seedSampleData;

    public SqliteFinanceStore(string databasePath, bool seedSampleData = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = Path.GetFullPath(databasePath);
        _seedSampleData = seedSampleData;
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _options = new DbContextOptionsBuilder<LocalFinanceDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options;
    }

    public static SqliteFinanceStore CreateDefault()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new SqliteFinanceStore(Path.Combine(appData, "NickFinancas", "nickfinancas.db"), seedSampleData: false);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        await database.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureCreditCardSchemaAsync(database, cancellationToken);
        await EnsureTransactionSchemaAsync(database, cancellationToken);
        await EnsureCategorySchemaAsync(database, cancellationToken);
        await EnsureFinancialAgendaSchemaAsync(database, cancellationToken);
        await EnsureInstallmentSchemaAsync(database, cancellationToken);
        await EnsureReserveSchemaAsync(database, cancellationToken);
        await EnsureGoalSchemaAsync(database, cancellationToken);
        await EnsureMemberSchemaAsync(database, cancellationToken);
        await EnsureSyncSchemaAsync(database, cancellationToken);
        if (!await database.FinancialSpaces.AnyAsync(cancellationToken))
        {
            database.FinancialSpaces.Add(new FinancialSpaceRecord
            {
                Id = PrimaryFinancialSpaceId, Name = "Casa & Futuro", IsPrimary = true,
            });
            await database.SaveChangesAsync(cancellationToken);
        }
        else if (!await database.FinancialSpaces.AnyAsync(item => item.IsPrimary, cancellationToken))
        {
            var firstSpace = await database.FinancialSpaces.OrderBy(item => item.Name).FirstAsync(cancellationToken);
            firstSpace.IsPrimary = true;
            await database.SaveChangesAsync(cancellationToken);
        }
        await EnsurePrimaryAccessAsync(cancellationToken);

        if (await database.Accounts.AnyAsync(cancellationToken))
        {
            if (!await database.Categories.AnyAsync(category => category.Id == TransferCategoryId, cancellationToken))
            {
                database.Categories.Add(new CategoryRecord
                {
                    Id = TransferCategoryId,
                    Name = "Transferencia entre contas",
                    Type = TransactionType.Transfer,
                });
                await database.SaveChangesAsync(cancellationToken);
            }
            await QueueInitialSyncSnapshotAsync(database, cancellationToken);
            return;
        }

        if (!_seedSampleData)
        {
            await QueueInitialSyncSnapshotAsync(database, cancellationToken);
            return;
        }

        database.Accounts.AddRange(
            new AccountRecord { Id = JointAccountId, Name = "Conta conjunta", Type = FinancialAccountType.Checking, InitialBalance = 6480.20m },
            new AccountRecord { Id = WalletAccountId, Name = "Carteira", Type = FinancialAccountType.Wallet, InitialBalance = 0m });

        database.Categories.AddRange(
            new CategoryRecord { Id = FoodCategoryId, Name = "Alimentacao", Type = TransactionType.Expense },
            new CategoryRecord { Id = HomeCategoryId, Name = "Casa", Type = TransactionType.Expense },
            new CategoryRecord { Id = SalaryCategoryId, Name = "Salario", Type = TransactionType.Income },
            new CategoryRecord { Id = TransferCategoryId, Name = "Transferencia entre contas", Type = TransactionType.Transfer });

        await database.SaveChangesAsync(cancellationToken);
        await QueueInitialSyncSnapshotAsync(database, cancellationToken);
    }

    internal DbContextOptions<LocalFinanceDbContext> SyncOptions => _options;

    public async Task EnsurePrimaryAccessAsync(CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        // Legacy schema migration stored the default SpaceId as a lowercase SQLite TEXT value.
        // EF Core writes Guid keys in uppercase, and SQLite compares these TEXT values by case.
        // The owner therefore exists but GetMembersAsync returns an empty list until normalized.
        await database.Database.ExecuteSqlRawAsync(
            "UPDATE members SET SpaceId = upper(SpaceId) WHERE SpaceId <> upper(SpaceId);",
            cancellationToken);
        var primarySpace = await database.FinancialSpaces
            .FirstOrDefaultAsync(item => item.IsPrimary, cancellationToken);
        if (primarySpace is null) return;

        // The original space has a fixed owner. Older sync data can contain other members
        // while that owner's record is missing, so checking whether the table is empty is insufficient.
        if (primarySpace.Id != PrimaryFinancialSpaceId) return;
        var owner = await database.Members.SingleOrDefaultAsync(item => item.Id == PrimaryMemberId, cancellationToken);
        if (owner is null)
        {
            var otherLogin = await database.Members.AsNoTracking().FirstOrDefaultAsync(
                item => item.Name == "Alex" && item.PinHash != null && item.PinSalt != null,
                cancellationToken);
            database.Members.Add(new MemberRecord
            {
                Id = PrimaryMemberId, SpaceId = PrimaryFinancialSpaceId, Name = "Alex", Email = "usuario@local",
                Role = FinancialSpaceRole.Administrator, IsPrimary = true,
                PinHash = otherLogin?.PinHash, PinSalt = otherLogin?.PinSalt,
            });
            await database.SaveChangesAsync(cancellationToken);
        }
        else if (owner.SpaceId != PrimaryFinancialSpaceId || owner.Role != FinancialSpaceRole.Administrator || !owner.IsPrimary)
        {
            owner.SpaceId = PrimaryFinancialSpaceId;
            owner.Role = FinancialSpaceRole.Administrator;
            owner.IsPrimary = true;
            await database.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task EnsureSyncSchemaAsync(
        LocalFinanceDbContext database,
        CancellationToken cancellationToken)
    {
        await database.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS sync_outbox (
                ChangeId TEXT NOT NULL CONSTRAINT PK_sync_outbox PRIMARY KEY,
                EntityType TEXT NOT NULL,
                EntityId TEXT NOT NULL,
                Operation TEXT NOT NULL,
                Payload TEXT NOT NULL,
                ChangedAtUtc TEXT NOT NULL,
                LocalVersion INTEGER NOT NULL,
                CreatedAtUtc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_sync_outbox_CreatedAtUtc ON sync_outbox (CreatedAtUtc);
            CREATE TABLE IF NOT EXISTS sync_state (
                Id INTEGER NOT NULL CONSTRAINT PK_sync_state PRIMARY KEY,
                LastPulledSequence INTEGER NOT NULL,
                LastSuccessfulSyncUtc TEXT NULL
            );
            INSERT OR IGNORE INTO sync_state (Id, LastPulledSequence, LastSuccessfulSyncUtc)
            VALUES (1, 0, NULL);
            """, cancellationToken);
    }

    private static async Task QueueInitialSyncSnapshotAsync(
        LocalFinanceDbContext database,
        CancellationToken cancellationToken)
    {
        var state = await database.SyncState.AsNoTracking().SingleAsync(item => item.Id == 1, cancellationToken);
        if (state.LastSuccessfulSyncUtc is not null || await database.SyncOutbox.AnyAsync(cancellationToken))
            return;

        var records = new List<object>();
        records.AddRange(await database.Accounts.AsNoTracking().ToListAsync(cancellationToken));
        records.AddRange(await database.Categories.AsNoTracking().ToListAsync(cancellationToken));
        records.AddRange(await database.Transactions.AsNoTracking().ToListAsync(cancellationToken));
        records.AddRange(await database.CreditCards.AsNoTracking().ToListAsync(cancellationToken));
        records.AddRange(await database.Payables.AsNoTracking().ToListAsync(cancellationToken));
        records.AddRange(await database.RecurringPayables.AsNoTracking().ToListAsync(cancellationToken));
        records.AddRange(await database.Reserves.AsNoTracking().ToListAsync(cancellationToken));
        records.AddRange(await database.ReserveMovements.AsNoTracking().ToListAsync(cancellationToken));
        records.AddRange(await database.AccountBalanceCorrections.AsNoTracking().ToListAsync(cancellationToken));
        records.AddRange(await database.Goals.AsNoTracking().ToListAsync(cancellationToken));
        records.AddRange(await database.FinancialSpaces.AsNoTracking().ToListAsync(cancellationToken));
        records.AddRange(await database.Members.AsNoTracking().ToListAsync(cancellationToken));

        var now = DateTimeOffset.UtcNow;
        foreach (var record in records)
        {
            var id = (Guid)(record.GetType().GetProperty("Id")?.GetValue(record)
                ?? throw new InvalidOperationException("Registro sincronizavel sem identificador."));
            database.SyncOutbox.Add(new SyncOutboxRecord
            {
                ChangeId = Guid.NewGuid(), EntityType = record.GetType().Name, EntityId = id,
                Operation = "upsert", Payload = JsonSerializer.Serialize(record, record.GetType()),
                ChangedAtUtc = now, LocalVersion = now.ToUnixTimeMilliseconds(), CreatedAtUtc = now,
            });
        }
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<DashboardSnapshot> GetDashboardAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);

        var accountRecords = await database.Accounts
            .AsNoTracking()
            .OrderBy(account => account.Name)
            .ToListAsync(cancellationToken);

        var categoryRecords = await database.Categories.AsNoTracking().OrderBy(category => category.Name)
            .ToListAsync(cancellationToken);
        var categories = categoryRecords.Select(category => new CategoryItem(
            category.Id, category.Name, category.Type, category.ParentCategoryId,
            category.ParentCategoryId is Guid parentId
                ? categoryRecords.FirstOrDefault(item => item.Id == parentId)?.Name
                : null)).ToList();

        var monthlyTransactions = await database.Transactions
            .AsNoTracking()
            .Where(transaction => !transaction.IsDeleted && transaction.Date >= start && transaction.Date < end)
            .ToListAsync(cancellationToken);

        var transactionsThroughPeriod = await database.Transactions
            .AsNoTracking()
            .Where(transaction => !transaction.IsDeleted && transaction.Date < end)
            .ToListAsync(cancellationToken);

        var balanceCorrections = await database.AccountBalanceCorrections.AsNoTracking()
            .Where(item => item.Date < end)
            .ToListAsync(cancellationToken);

        var accounts = accountRecords
            .Select(account => new AccountItem(
                account.Id,
                account.Name,
                account.Type,
                account.InitialBalance,
                CalculateAccountBalance(account.Id, account.InitialBalance, transactionsThroughPeriod) -
                balanceCorrections.Where(item => item.AccountId == account.Id).Sum(item => item.Amount)))
            .ToList();

        var recentTransactions = await database.Transactions
            .AsNoTracking()
            .Where(transaction => !transaction.IsDeleted)
            .OrderByDescending(transaction => transaction.Date)
            .ThenByDescending(transaction => transaction.CreatedAtUtc)
            .Take(8)
            .Select(transaction => new TransactionItem(
                transaction.Id,
                transaction.AccountId,
                transaction.CategoryId,
                transaction.Type,
                transaction.Description,
                transaction.Amount,
                transaction.Date,
                transaction.AuthorName,
                transaction.DestinationAccountId,
                transaction.CreditCardId,
                transaction.InvoiceYear,
                transaction.InvoiceMonth,
                transaction.DeviceName))
            .ToListAsync(cancellationToken);

        var income = monthlyTransactions
            .Where(transaction => transaction.Type == TransactionType.Income)
            .Sum(transaction => transaction.Amount);
        var expense = monthlyTransactions
            .Where(transaction => transaction.Type == TransactionType.Expense)
            .Sum(transaction => transaction.Amount);
        // Compras no credito comprometem o limite do cartao, nao o dinheiro das contas.
        // O saldo bancario so e reduzido quando o pagamento da fatura e registrado.
        var reserved = await database.ReserveMovements.AsNoTracking()
            .Where(item => item.Date < end)
            .SumAsync(item => item.IsDeposit ? item.Amount : -item.Amount, cancellationToken);
        var availableBalance = accounts.Sum(account => account.CurrentBalance) - reserved;

        return new DashboardSnapshot(
            availableBalance,
            income,
            expense,
            reserved,
            accounts,
            categories,
            recentTransactions);
    }

    public async Task AddTransactionAsync(
        CreateTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        var installmentCount = request.InstallmentCount;
        if (installmentCount is < 1 or > 60)
            throw new ArgumentException("A quantidade de parcelas deve ficar entre 1 e 60.");
        if (request.InstallmentStart < 1 || request.InstallmentStart > installmentCount)
            throw new ArgumentException("A primeira parcela deve estar entre 1 e o total de parcelas.");
        if (installmentCount > 1 && request.Type != TransactionType.Expense)
            throw new ArgumentException("Somente despesas podem ser parceladas.");
        var cardData = await ResolveCardTransactionAsync(
            database, request.Type, request.CreditCardId, request.Date, cancellationToken);
        var accountId = cardData?.PaymentAccountId ?? request.AccountId;
        ValidateTransaction(accountId, request.CategoryId, request.Description, request.Amount,
            request.Type, request.DestinationAccountId);
        await EnsureTransferAccountsExistAsync(
            database, request.Type, accountId, request.DestinationAccountId, cancellationToken);
        var baseAmount = Math.Floor(request.Amount / installmentCount * 100m) / 100m;
        if (baseAmount <= 0m)
            throw new ArgumentException("O valor de cada parcela deve ser pelo menos R$ 0,01.");
        var groupId = installmentCount > 1 ? Guid.NewGuid() : (Guid?)null;
        for (var installment = request.InstallmentStart; installment <= installmentCount; installment++)
        {
            var installmentDate = request.CreditCardId is null
                ? request.Date.AddMonths(installment - request.InstallmentStart)
                : request.Date.AddMonths(installment - 1);
            var installmentAmount = installment == installmentCount
                ? request.Amount - baseAmount * (installmentCount - 1) : baseAmount;
            if (request.CreditCardId is null && installment > request.InstallmentStart)
            {
                database.Payables.Add(new PayableRecord
                {
                    Id = Guid.NewGuid(), Description = request.Description.Trim(),
                    Amount = installmentAmount, DueDate = installmentDate,
                    AccountId = accountId, CategoryId = request.CategoryId,
                    InstallmentGroupId = groupId, InstallmentNumber = installment,
                    InstallmentTotal = installmentCount,
                });
                continue;
            }
            var installmentCardData = request.CreditCardId is null ? null :
                await ResolveCardTransactionAsync(database, request.Type, request.CreditCardId,
                    installmentDate, cancellationToken);
            database.Transactions.Add(new TransactionRecord
            {
                Id = Guid.NewGuid(), AccountId = accountId,
                CategoryId = request.Type == TransactionType.Transfer ? TransferCategoryId : request.CategoryId,
                DestinationAccountId = request.DestinationAccountId, CreditCardId = request.CreditCardId,
                InvoiceYear = installmentCardData?.InvoiceYear, InvoiceMonth = installmentCardData?.InvoiceMonth,
                InstallmentGroupId = groupId, InstallmentNumber = groupId is null ? null : installment,
                InstallmentTotal = groupId is null ? null : installmentCount,
                Type = request.Type,
                Description = installmentCount == 1 ? request.Description.Trim() : $"{request.Description.Trim()} ({installment}/{installmentCount})",
                Amount = installmentAmount, Date = installmentDate, AuthorId = request.AuthorId,
                AuthorName = request.AuthorName, DeviceName = Environment.MachineName,
                CreatedAtUtc = DateTime.UtcNow.AddTicks(installment), Version = 1,
            });
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TransactionItem>> GetTransactionsAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);

        return await database.Transactions
            .AsNoTracking()
            .Where(transaction => !transaction.IsDeleted && transaction.Date >= start && transaction.Date < end)
            .OrderByDescending(transaction => transaction.Date)
            .ThenByDescending(transaction => transaction.CreatedAtUtc)
            .Select(transaction => new TransactionItem(
                transaction.Id,
                transaction.AccountId,
                transaction.CategoryId,
                transaction.Type,
                transaction.Description,
                transaction.Amount,
                transaction.Date,
                transaction.AuthorName,
                transaction.DestinationAccountId,
                transaction.CreditCardId,
                transaction.InvoiceYear,
                transaction.InvoiceMonth,
                transaction.DeviceName))
            .ToListAsync(cancellationToken);
    }

    public async Task<TransactionExportResult> ExportTransactionsCsvAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        var transactions = await GetTransactionsAsync(year, month, cancellationToken);
        await using var database = new LocalFinanceDbContext(_options);
        var accounts = await database.Accounts.AsNoTracking()
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var categories = await database.Categories.AsNoTracking()
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var cards = await database.CreditCards.AsNoTracking()
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine("Data;Tipo;Descricao;Categoria;Origem;Destino;Cartao;Valor;Autor;Dispositivo");
        foreach (var transaction in transactions.OrderBy(item => item.Date).ThenBy(item => item.Description))
        {
            var type = transaction.Type switch
            {
                TransactionType.Income => "Receita",
                TransactionType.Expense => "Despesa",
                TransactionType.Transfer => "Transferencia",
                TransactionType.CreditCardPayment => "Pagamento de fatura",
                _ => transaction.Type.ToString(),
            };
            var values = new[]
            {
                transaction.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                type,
                transaction.Description,
                categories.GetValueOrDefault(transaction.CategoryId, "Categoria removida"),
                accounts.GetValueOrDefault(transaction.AccountId, "Conta removida"),
                transaction.DestinationAccountId is Guid destinationId
                    ? accounts.GetValueOrDefault(destinationId, "Conta removida") : string.Empty,
                transaction.CreditCardId is Guid cardId
                    ? cards.GetValueOrDefault(cardId, "Cartao removido") : string.Empty,
                transaction.Amount.ToString("0.00", CultureInfo.GetCultureInfo("pt-BR")),
                transaction.AuthorName,
                transaction.DeviceName,
            };
            csv.AppendLine(string.Join(';', values.Select(EscapeCsv)));
        }

        var exportDirectory = Path.Combine(Path.GetDirectoryName(_databasePath)!, "Exports");
        Directory.CreateDirectory(exportDirectory);
        var exportPath = Path.Combine(exportDirectory, $"lancamentos-{year:D4}-{month:D2}-{DateTime.Now:yyyyMMdd-HHmmssfff}.csv");
        await File.WriteAllTextAsync(exportPath, csv.ToString(), new UTF8Encoding(true), cancellationToken);
        return new TransactionExportResult(exportPath, transactions.Count);
    }

    private static string EscapeCsv(string value) =>
        $"\"{value.Replace("\"", "\"\"")}\"";

    public async Task UpdateTransactionAsync(
        UpdateTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        var cardData = await ResolveCardTransactionAsync(
            database, request.Type, request.CreditCardId, request.Date, cancellationToken);
        var accountId = cardData?.PaymentAccountId ?? request.AccountId;
        ValidateTransaction(accountId, request.CategoryId, request.Description, request.Amount,
            request.Type, request.DestinationAccountId);
        await EnsureTransferAccountsExistAsync(
            database, request.Type, accountId, request.DestinationAccountId, cancellationToken);
        var transaction = await database.Transactions.SingleOrDefaultAsync(
                item => item.Id == request.Id && !item.IsDeleted,
                cancellationToken)
            ?? throw new InvalidOperationException("Lancamento nao encontrado.");

        transaction.AccountId = accountId;
        transaction.CategoryId = request.Type == TransactionType.Transfer ? TransferCategoryId : request.CategoryId;
        transaction.DestinationAccountId = request.DestinationAccountId;
        transaction.CreditCardId = request.CreditCardId;
        transaction.InvoiceYear = cardData?.InvoiceYear;
        transaction.InvoiceMonth = cardData?.InvoiceMonth;
        transaction.Type = request.Type;
        transaction.Description = request.Description.Trim();
        transaction.Amount = request.Amount;
        transaction.Date = request.Date;
        transaction.Version++;
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteTransactionAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        var transaction = await database.Transactions.SingleOrDefaultAsync(
                item => item.Id == transactionId && !item.IsDeleted,
                cancellationToken)
            ?? throw new InvalidOperationException("Lancamento nao encontrado.");

        transaction.IsDeleted = true;
        transaction.Version++;
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task AddAccountAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("O nome da conta e obrigatorio.", nameof(request));
        }
        if (request.InitialBalance is < -MaxMonetaryAmount or > MaxMonetaryAmount)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "O saldo inicial esta fora do limite permitido.");
        }

        await using var database = new LocalFinanceDbContext(_options);
        if (await database.Accounts.AnyAsync(account => account.Name.ToLower() == name.ToLower(), cancellationToken))
        {
            throw new InvalidOperationException("Ja existe uma conta com esse nome.");
        }

        database.Accounts.Add(new AccountRecord
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = request.Type,
            InitialBalance = request.InitialBalance,
        });

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task AddCategoryAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("O nome da categoria e obrigatorio.", nameof(request));
        }

        await using var database = new LocalFinanceDbContext(_options);
        if (request.ParentCategoryId is Guid parentId)
        {
            var validParent = await database.Categories.AnyAsync(
                category => category.Id == parentId && category.Type == request.Type && category.ParentCategoryId == null,
                cancellationToken);
            if (!validParent) throw new InvalidOperationException("A categoria principal deve existir e ter o mesmo tipo.");
        }
        if (await database.Categories.AnyAsync(
                category => category.Type == request.Type && category.ParentCategoryId == request.ParentCategoryId &&
                            category.Name.ToLower() == name.ToLower(),
                cancellationToken))
        {
            throw new InvalidOperationException("Ja existe uma categoria desse tipo com esse nome.");
        }

        database.Categories.Add(new CategoryRecord
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = request.Type,
            ParentCategoryId = request.ParentCategoryId,
        });

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        if (await database.Members.AnyAsync(item => item.SalaryAccountId == accountId, cancellationToken))
            throw new InvalidOperationException("Essa conta recebe um salario automatico. Altere a conta do salario antes de exclui-la.");
        if (await database.Transactions.AnyAsync(transaction =>
                !transaction.IsDeleted &&
                (transaction.AccountId == accountId || transaction.DestinationAccountId == accountId),
                cancellationToken))
        {
            throw new InvalidOperationException("Essa conta possui lancamentos e nao pode ser excluida.");
        }

        if (await database.CreditCards.AnyAsync(card => card.PaymentAccountId == accountId, cancellationToken))
        {
            throw new InvalidOperationException("Essa conta esta vinculada a um cartao e nao pode ser excluida.");
        }

        if (await database.AccountBalanceCorrections.AnyAsync(item => item.AccountId == accountId, cancellationToken))
        {
            throw new InvalidOperationException("Essa conta possui uma correcao de saldo e nao pode ser excluida.");
        }

        if (await database.Payables.AnyAsync(item => item.AccountId == accountId && !item.IsDeleted, cancellationToken) ||
            await database.RecurringPayables.AnyAsync(item => item.AccountId == accountId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("Essa conta esta vinculada a pendencias e nao pode ser excluida.");
        }

        if (await database.Accounts.CountAsync(cancellationToken) <= 1)
        {
            throw new InvalidOperationException("O aplicativo precisa manter pelo menos uma conta.");
        }

        var account = await database.Accounts.SingleOrDefaultAsync(item => item.Id == accountId, cancellationToken)
            ?? throw new InvalidOperationException("Conta nao encontrada.");
        database.Transactions.RemoveRange(await database.Transactions.Where(transaction =>
            transaction.IsDeleted &&
            (transaction.AccountId == accountId || transaction.DestinationAccountId == accountId))
            .ToListAsync(cancellationToken));
        database.Accounts.Remove(account);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        var activeTransactionDates = await database.Transactions.AsNoTracking()
            .Where(transaction => transaction.CategoryId == categoryId && !transaction.IsDeleted)
            .OrderBy(transaction => transaction.Date)
            .Select(transaction => transaction.Date)
            .ToListAsync(cancellationToken);
        if (activeTransactionDates.Count > 0)
        {
            var firstDate = activeTransactionDates[0].ToString("dd/MM/yyyy", BrazilianCulture);
            var lastDate = activeTransactionDates[^1].ToString("dd/MM/yyyy", BrazilianCulture);
            var period = firstDate == lastDate ? firstDate : $"{firstDate} a {lastDate}";
            throw new InvalidOperationException(
                $"Essa categoria ainda possui {activeTransactionDates.Count} lancamento(s) ativo(s) " +
                $"em {period}. Confira outros meses antes de exclui-la.");
        }

        if (await database.Categories.AnyAsync(category => category.ParentCategoryId == categoryId, cancellationToken))
        {
            throw new InvalidOperationException("Essa categoria possui subcategorias e nao pode ser excluida.");
        }

        var payableDates = await database.Payables.AsNoTracking()
            .Where(item => item.CategoryId == categoryId && !item.IsDeleted)
            .OrderBy(item => item.DueDate)
            .Select(item => item.DueDate)
            .ToListAsync(cancellationToken);
        if (payableDates.Count > 0)
        {
            var firstDate = payableDates[0].ToString("dd/MM/yyyy", BrazilianCulture);
            var lastDate = payableDates[^1].ToString("dd/MM/yyyy", BrazilianCulture);
            var period = firstDate == lastDate ? firstDate : $"{firstDate} a {lastDate}";
            throw new InvalidOperationException(
                $"Essa categoria ainda possui {payableDates.Count} pendencia(s) vinculada(s) em {period}. " +
                "Confira outros meses antes de exclui-la.");
        }

        if (await database.RecurringPayables.AnyAsync(
                item => item.CategoryId == categoryId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException(
                "Essa categoria pertence a uma recorrencia ativa. Encerre a recorrencia antes de exclui-la.");
        }

        var category = await database.Categories.SingleOrDefaultAsync(item => item.Id == categoryId, cancellationToken)
            ?? throw new InvalidOperationException("Categoria nao encontrada.");
        // Deleted transactions are tombstones for sync, not visible financial entries. Remove
        // them with tracked deletes so a category used only by tombstones can be removed safely.
        database.Transactions.RemoveRange(await database.Transactions.Where(transaction =>
            transaction.CategoryId == categoryId && transaction.IsDeleted)
            .ToListAsync(cancellationToken));
        database.Categories.Remove(category);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CreditCardItem>> GetCreditCardsAsync(CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        return await database.CreditCards
            .AsNoTracking()
            .OrderBy(card => card.Name)
            .Select(card => new CreditCardItem(
                card.Id,
                card.Name,
                card.PaymentAccountId,
                card.CreditLimit,
                card.SpendingLimit,
                card.ClosingDay,
                card.DueDay))
            .ToListAsync(cancellationToken);
    }

    public async Task AddCreditCardAsync(
        CreateCreditCardRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("O nome do cartao e obrigatorio.", nameof(request));
        }

        if (request.PaymentAccountId == Guid.Empty || request.CreditLimit <= 0 || request.CreditLimit > MaxMonetaryAmount)
        {
            throw new ArgumentException("Conta de pagamento e limite sao obrigatorios.", nameof(request));
        }

        if (request.ClosingDay is < 1 or > 31 || request.DueDay is < 1 or > 31)
        {
            throw new ArgumentException("Fechamento e vencimento devem estar entre os dias 1 e 31.", nameof(request));
        }

        await using var database = new LocalFinanceDbContext(_options);
        if (await database.CreditCards.AnyAsync(card => card.Name.ToLower() == name.ToLower(), cancellationToken))
        {
            throw new InvalidOperationException("Ja existe um cartao com esse nome.");
        }

        database.CreditCards.Add(new CreditCardRecord
        {
            Id = Guid.NewGuid(),
            Name = name,
            PaymentAccountId = request.PaymentAccountId,
            CreditLimit = request.CreditLimit,
            SpendingLimit = request.CreditLimit,
            ClosingDay = request.ClosingDay,
            DueDay = request.DueDay,
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateCreditCardLimitAsync(
        UpdateCreditCardLimitRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CreditCardId == Guid.Empty || request.CreditLimit <= 0m ||
            request.CreditLimit > MaxMonetaryAmount)
        {
            throw new ArgumentException("Informe um limite valido para o cartao.", nameof(request));
        }

        await using var database = new LocalFinanceDbContext(_options);
        var card = await database.CreditCards.SingleOrDefaultAsync(
                item => item.Id == request.CreditCardId,
                cancellationToken)
            ?? throw new InvalidOperationException("Cartao nao encontrado.");

        var cardTransactions = await database.Transactions
            .Where(item => !item.IsDeleted && item.CreditCardId == card.Id)
            .ToListAsync(cancellationToken);
        var usedLimit = Math.Max(0m,
            cardTransactions.Where(item => item.Type == TransactionType.Expense).Sum(item => item.Amount) -
            cardTransactions.Where(item => item.Type == TransactionType.CreditCardPayment).Sum(item => item.Amount));
        if (request.CreditLimit < usedLimit || request.CreditLimit > card.CreditLimit)
        {
            throw new InvalidOperationException("O limite de uso deve ficar entre o valor ja utilizado e o limite total do banco.");
        }

        card.SpendingLimit = request.CreditLimit;
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateCreditCardAsync(
        UpdateCreditCardRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        if (request.CreditCardId == Guid.Empty || string.IsNullOrWhiteSpace(name) ||
            request.PaymentAccountId == Guid.Empty || request.CreditLimit <= 0m ||
            request.CreditLimit > MaxMonetaryAmount || request.ClosingDay is < 1 or > 31 ||
            request.DueDay is < 1 or > 31)
        {
            throw new ArgumentException("Preencha corretamente os dados do cartao.", nameof(request));
        }

        await using var database = new LocalFinanceDbContext(_options);
        var card = await database.CreditCards.SingleOrDefaultAsync(
                item => item.Id == request.CreditCardId, cancellationToken)
            ?? throw new InvalidOperationException("Cartao nao encontrado.");
        if (await database.CreditCards.AnyAsync(
                item => item.Id != request.CreditCardId && item.Name.ToLower() == name.ToLower(),
                cancellationToken))
        {
            throw new InvalidOperationException("Ja existe um cartao com esse nome.");
        }
        if (!await database.Accounts.AnyAsync(item => item.Id == request.PaymentAccountId, cancellationToken))
        {
            throw new InvalidOperationException("Conta de pagamento nao encontrada.");
        }

        var transactions = await database.Transactions
            .Where(item => !item.IsDeleted && item.CreditCardId == card.Id)
            .ToListAsync(cancellationToken);
        var usedLimit = Math.Max(0m,
            transactions.Where(item => item.Type == TransactionType.Expense).Sum(item => item.Amount) -
            transactions.Where(item => item.Type == TransactionType.CreditCardPayment).Sum(item => item.Amount));
        if (request.CreditLimit < usedLimit)
        {
            throw new InvalidOperationException("O limite total nao pode ser menor que o valor ja utilizado.");
        }

        card.Name = name;
        card.PaymentAccountId = request.PaymentAccountId;
        card.CreditLimit = request.CreditLimit;
        card.SpendingLimit = Math.Max(usedLimit, Math.Min(card.SpendingLimit, request.CreditLimit));
        card.ClosingDay = request.ClosingDay;
        card.DueDay = request.DueDay;
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteCreditCardAsync(Guid creditCardId, CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        if (await database.Transactions.AnyAsync(
                transaction => transaction.CreditCardId == creditCardId && !transaction.IsDeleted,
                cancellationToken))
        {
            throw new InvalidOperationException("Esse cartao possui compras ou pagamentos e nao pode ser excluido.");
        }
        var card = await database.CreditCards.SingleOrDefaultAsync(item => item.Id == creditCardId, cancellationToken)
            ?? throw new InvalidOperationException("Cartao nao encontrado.");
        database.CreditCards.Remove(card);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CreditCardInvoiceItem>> GetCreditCardInvoicesAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        var cards = await database.CreditCards.AsNoTracking().OrderBy(card => card.Name).ToListAsync(cancellationToken);
        var cardIds = cards.Select(card => card.Id).ToList();
        var transactions = await database.Transactions.AsNoTracking()
            .Where(transaction => !transaction.IsDeleted && transaction.CreditCardId != null &&
                                  cardIds.Contains(transaction.CreditCardId.Value))
            .ToListAsync(cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.Today);

        return cards.Select(card =>
        {
            var cardTransactions = transactions.Where(item => item.CreditCardId == card.Id).ToList();
            var invoiceTransactions = cardTransactions
                .Where(item => item.InvoiceYear == year && item.InvoiceMonth == month)
                .ToList();
            var total = invoiceTransactions.Where(item => item.Type == TransactionType.Expense).Sum(item => item.Amount);
            var paid = invoiceTransactions.Where(item => item.Type == TransactionType.CreditCardPayment).Sum(item => item.Amount);
            var outstanding = Math.Max(0m, total - paid);
            var usedLimit = Math.Max(0m,
                cardTransactions.Where(item => item.Type == TransactionType.Expense).Sum(item => item.Amount) -
                cardTransactions.Where(item => item.Type == TransactionType.CreditCardPayment).Sum(item => item.Amount));
            var closingReference = card.DueDay > card.ClosingDay
                ? new DateOnly(year, month, 1)
                : new DateOnly(year, month, 1).AddMonths(-1);
            var closingDate = SafeDate(closingReference.Year, closingReference.Month, card.ClosingDay);
            var dueDate = SafeDate(year, month, card.DueDay);
            var status = outstanding == 0m && total > 0m ? "Paga" :
                today > dueDate && outstanding > 0m ? "Vencida" :
                today > closingDate ? "Fechada" : "Aberta";

            return new CreditCardInvoiceItem(
                card.Id, card.Name, card.SpendingLimit, card.CreditLimit, usedLimit,
                Math.Max(0m, card.SpendingLimit - usedLimit),
                year, month, closingDate, dueDate, total, paid, outstanding, status);
        }).ToList();
    }

    public async Task PayCreditCardInvoiceAsync(
        PayCreditCardInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        var card = await database.CreditCards.SingleOrDefaultAsync(item => item.Id == request.CreditCardId, cancellationToken)
            ?? throw new InvalidOperationException("Cartao nao encontrado.");
        var invoiceTransactions = await database.Transactions
            .Where(item => !item.IsDeleted && item.CreditCardId == request.CreditCardId &&
                           item.InvoiceYear == request.InvoiceYear && item.InvoiceMonth == request.InvoiceMonth)
            .ToListAsync(cancellationToken);
        var outstanding =
            invoiceTransactions.Where(item => item.Type == TransactionType.Expense).Sum(item => item.Amount) -
            invoiceTransactions.Where(item => item.Type == TransactionType.CreditCardPayment).Sum(item => item.Amount);
        if (outstanding <= 0m)
        {
            throw new InvalidOperationException("Esta fatura nao possui valor pendente.");
        }

        database.Transactions.Add(new TransactionRecord
        {
            Id = Guid.NewGuid(), AccountId = card.PaymentAccountId, CategoryId = TransferCategoryId,
            CreditCardId = card.Id, InvoiceYear = request.InvoiceYear, InvoiceMonth = request.InvoiceMonth,
            Type = TransactionType.CreditCardPayment, Description = $"Pagamento da fatura {card.Name}",
            Amount = outstanding, Date = request.PaymentDate, AuthorId = request.AuthorId,
            AuthorName = request.AuthorName, DeviceName = Environment.MachineName,
            CreatedAtUtc = DateTime.UtcNow, Version = 1,
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PayableItem>> GetPayablesAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        await GenerateRecurringPayablesAsync(database, year, month, cancellationToken);
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var payables = await database.Payables.AsNoTracking()
            .Where(item => !item.IsDeleted && item.DueDate >= start && item.DueDate < end)
            .OrderBy(item => item.DueDate)
            .ThenBy(item => item.Description)
            .ToListAsync(cancellationToken);
        return payables.Select(item => new PayableItem(
            item.Id, FormatInstallmentDescription(item.Description, item.InstallmentNumber, item.InstallmentTotal),
            item.Amount, item.DueDate, item.AccountId, item.CategoryId,
            item.PaidDate is not null ? "Paga" : item.DueDate < today ? "Vencida" : "Pendente",
            item.RecurringPayableId is not null, item.PaidDate)).ToList();
    }

    public async Task AddPayableAsync(CreatePayableRequest request, CancellationToken cancellationToken = default)
    {
        ValidatePayable(request.Description, request.Amount, request.AccountId, request.CategoryId);
        if (request.InstallmentCount is < 1 or > 60 ||
            request.InstallmentStart < 1 || request.InstallmentStart > request.InstallmentCount)
            throw new ArgumentException("Informe o total de parcelas e a primeira parcela em aberto entre 1 e 60.");
        await using var database = new LocalFinanceDbContext(_options);
        await EnsurePayableReferencesAsync(database, request.AccountId, request.CategoryId, cancellationToken);
        var groupId = request.InstallmentCount > 1 ? Guid.NewGuid() : (Guid?)null;
        for (var number = request.InstallmentStart; number <= request.InstallmentCount; number++)
        {
            database.Payables.Add(new PayableRecord
            {
                Id = Guid.NewGuid(), Description = request.Description.Trim(), Amount = request.Amount,
                DueDate = request.DueDate.AddMonths(number - request.InstallmentStart),
                AccountId = request.AccountId, CategoryId = request.CategoryId,
                InstallmentGroupId = groupId, InstallmentNumber = groupId is null ? null : number,
                InstallmentTotal = groupId is null ? null : request.InstallmentCount,
            });
        }
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InstallmentPlanItem>> GetInstallmentPlansAsync(
        CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        var payables = await database.Payables.AsNoTracking()
            .Where(item => !item.IsDeleted && item.InstallmentGroupId != null && item.PaidDate == null)
            .ToListAsync(cancellationToken);
        var plans = payables.GroupBy(item => item.InstallmentGroupId!.Value)
            .Select(group =>
            {
                var next = group.OrderBy(item => item.DueDate).First();
                return new InstallmentPlanItem(group.Key, next.Description, "Conta",
                    next.InstallmentTotal!.Value, group.Count(), next.DueDate, next.Amount, true);
            }).ToList();

        var cardTransactions = await database.Transactions.AsNoTracking()
            .Where(item => !item.IsDeleted && item.CreditCardId != null)
            .ToListAsync(cancellationToken);
        var cards = await database.CreditCards.AsNoTracking()
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var invoiceBalances = cardTransactions
            .Where(item => item.InvoiceYear != null && item.InvoiceMonth != null)
            .GroupBy(item => (item.CreditCardId!.Value, item.InvoiceYear!.Value, item.InvoiceMonth!.Value))
            .ToDictionary(group => group.Key,
                group => group.Where(item => item.Type == TransactionType.Expense).Sum(item => item.Amount) -
                         group.Where(item => item.Type == TransactionType.CreditCardPayment).Sum(item => item.Amount));
        foreach (var group in cardTransactions.Where(item => item.Type == TransactionType.Expense &&
                     item.InstallmentGroupId != null).GroupBy(item => item.InstallmentGroupId!.Value))
        {
            var unpaid = group.Where(item => item.InvoiceYear is int year && item.InvoiceMonth is int month &&
                invoiceBalances.GetValueOrDefault((item.CreditCardId!.Value, year, month)) > 0m).ToList();
            if (unpaid.Count == 0) continue;
            var next = unpaid.OrderBy(item => item.InvoiceYear).ThenBy(item => item.InvoiceMonth).First();
            if (!cards.TryGetValue(next.CreditCardId!.Value, out var card)) continue;
            plans.Add(new InstallmentPlanItem(group.Key,
                BaseInstallmentDescription(next.Description, next.InstallmentNumber, next.InstallmentTotal),
                card.Name, next.InstallmentTotal!.Value, unpaid.Count,
                SafeDate(next.InvoiceYear!.Value, next.InvoiceMonth!.Value, card.DueDay), next.Amount, false));
        }
        return plans.OrderBy(item => item.NextDueDate).ThenBy(item => item.Description).ToList();
    }

    public async Task DeleteRemainingInstallmentsAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        var remaining = await database.Payables
            .Where(item => !item.IsDeleted && item.InstallmentGroupId == groupId && item.PaidDate == null)
            .ToListAsync(cancellationToken);
        if (remaining.Count == 0)
            throw new InvalidOperationException("Nenhuma parcela pendente foi encontrada para esta compra.");
        database.Payables.RemoveRange(remaining);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRecurringPayableAsync(
        CreateRecurringPayableRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidatePayable(request.Description, request.Amount, request.AccountId, request.CategoryId);
        if (request.DueDay is < 1 or > 31 || request.EndDate < request.StartDate)
        {
            throw new ArgumentException("Informe um vencimento e um periodo validos.");
        }
        await using var database = new LocalFinanceDbContext(_options);
        await EnsurePayableReferencesAsync(database, request.AccountId, request.CategoryId, cancellationToken);
        var rule = new RecurringPayableRecord
        {
            Id = Guid.NewGuid(), Description = request.Description.Trim(), Amount = request.Amount,
            DueDay = request.DueDay, StartDate = request.StartDate, EndDate = request.EndDate,
            AccountId = request.AccountId, CategoryId = request.CategoryId, IsActive = true,
        };
        database.RecurringPayables.Add(rule);
        await database.SaveChangesAsync(cancellationToken);
        await GenerateRecurringPayablesAsync(database, request.StartDate.Year, request.StartDate.Month, cancellationToken);
    }

    public async Task PayPayableAsync(PayPayableRequest request, CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        var payable = await database.Payables.SingleOrDefaultAsync(item => item.Id == request.PayableId && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Conta pendente nao encontrada.");
        if (payable.PaidDate is not null)
        {
            throw new InvalidOperationException("Esta conta ja foi paga.");
        }
        var transactionId = Guid.NewGuid();
        database.Transactions.Add(new TransactionRecord
        {
            Id = transactionId, AccountId = payable.AccountId, CategoryId = payable.CategoryId,
            Type = TransactionType.Expense,
            Description = FormatInstallmentDescription(payable.Description, payable.InstallmentNumber, payable.InstallmentTotal),
            Amount = payable.Amount,
            InstallmentGroupId = payable.InstallmentGroupId, InstallmentNumber = payable.InstallmentNumber,
            InstallmentTotal = payable.InstallmentTotal,
            Date = request.PaymentDate, AuthorId = request.AuthorId, AuthorName = request.AuthorName,
            DeviceName = Environment.MachineName, CreatedAtUtc = DateTime.UtcNow, Version = 1,
        });
        payable.PaidDate = request.PaymentDate;
        payable.PaidTransactionId = transactionId;
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task DeletePayableAsync(Guid payableId, CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        var payable = await database.Payables.SingleOrDefaultAsync(item => item.Id == payableId && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Conta pendente nao encontrada.");
        if (payable.PaidDate is not null)
        {
            if (payable.PaidTransactionId is not Guid transactionId)
                throw new InvalidOperationException("O lancamento do pagamento nao foi encontrado. A pendencia nao foi excluida.");
            var transaction = await database.Transactions.SingleOrDefaultAsync(
                item => item.Id == transactionId, cancellationToken);
            if (transaction is null)
                throw new InvalidOperationException("O lancamento do pagamento nao foi encontrado. A pendencia nao foi excluida.");
            if (!transaction.IsDeleted)
            {
                transaction.IsDeleted = true;
                transaction.Version++;
            }
        }
        // Keep recurring occurrences as tombstones so loading the month cannot recreate them.
        payable.IsDeleted = true;
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecurringPayableItem>> GetRecurringPayablesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        return await database.RecurringPayables.AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.DueDay)
            .ThenBy(item => item.Description)
            .Select(item => new RecurringPayableItem(
                item.Id, item.Description, item.Amount, item.DueDay, item.StartDate, item.EndDate,
                item.AccountId, item.CategoryId))
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteRecurringPayableAsync(
        Guid recurringPayableId,
        CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        var rule = await database.RecurringPayables.SingleOrDefaultAsync(
                item => item.Id == recurringPayableId && item.IsActive,
                cancellationToken)
            ?? throw new InvalidOperationException("Recorrencia nao encontrada.");
        rule.IsActive = false;
        var unpaidOccurrences = await database.Payables
            .Where(item => item.RecurringPayableId == recurringPayableId && item.PaidDate == null && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        database.Payables.RemoveRange(unpaidOccurrences);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReserveItem>> GetReservesAsync(CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        var reserves = await database.Reserves.AsNoTracking().OrderBy(item => item.Name).ToListAsync(cancellationToken);
        var movements = await database.ReserveMovements.AsNoTracking().ToListAsync(cancellationToken);
        return reserves.Select(reserve => new ReserveItem(
            reserve.Id, reserve.Name,
            movements.Where(item => item.ReserveId == reserve.Id)
                .Sum(item => item.IsDeposit ? item.Amount : -item.Amount))).ToList();
    }

    public async Task AddReserveAsync(CreateReserveRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Informe o nome da reserva.");
        await using var database = new LocalFinanceDbContext(_options);
        database.Reserves.Add(new ReserveRecord { Id = Guid.NewGuid(), Name = request.Name.Trim() });
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateReserveAsync(UpdateReserveRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ArgumentException("Informe o nome da reserva.");
        await using var database = new LocalFinanceDbContext(_options);
        var reserve = await database.Reserves.SingleOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException("Reserva nao encontrada.");
        reserve.Name = request.Name.Trim();
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteReserveAsync(Guid reserveId, CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        if (await database.ReserveMovements.AnyAsync(item => item.ReserveId == reserveId, cancellationToken))
            throw new InvalidOperationException("A reserva possui movimentacoes e nao pode ser excluida.");
        if (await database.Goals.AnyAsync(item => item.ReserveId == reserveId, cancellationToken))
            throw new InvalidOperationException("A reserva esta vinculada a um objetivo e nao pode ser excluida.");
        var reserve = await database.Reserves.SingleOrDefaultAsync(item => item.Id == reserveId, cancellationToken)
            ?? throw new InvalidOperationException("Reserva nao encontrada.");
        database.Reserves.Remove(reserve);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task AddReserveMovementAsync(
        AddReserveMovementRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0m || request.Amount > MaxMonetaryAmount)
            throw new ArgumentException("Informe um valor valido para a movimentacao.");
        await using var database = new LocalFinanceDbContext(_options);
        if (!await database.Reserves.AnyAsync(item => item.Id == request.ReserveId, cancellationToken) ||
            !await database.Accounts.AnyAsync(item => item.Id == request.AccountId, cancellationToken))
            throw new InvalidOperationException("Reserva ou conta nao encontrada.");
        var balance = await database.ReserveMovements.Where(item => item.ReserveId == request.ReserveId)
            .SumAsync(item => item.IsDeposit ? item.Amount : -item.Amount, cancellationToken);
        if (!request.IsDeposit && request.Amount > balance)
            throw new InvalidOperationException("A retirada nao pode ser maior que o saldo da reserva.");
        database.ReserveMovements.Add(new ReserveMovementRecord
        {
            Id = Guid.NewGuid(), ReserveId = request.ReserveId, AccountId = request.AccountId,
            Amount = request.Amount, IsDeposit = request.IsDeposit, Date = request.Date,
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveReserveDepositWithoutBalanceIncreaseAsync(
        Guid movementId, CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var movement = await database.ReserveMovements.SingleOrDefaultAsync(
            item => item.Id == movementId, cancellationToken)
            ?? throw new InvalidOperationException("Movimentacao da reserva nao encontrada.");
        if (!movement.IsDeposit)
            throw new InvalidOperationException("Somente um aporte pode ser corrigido desta forma.");
        var balance = await database.ReserveMovements.Where(item => item.ReserveId == movement.ReserveId)
            .SumAsync(item => item.IsDeposit ? item.Amount : -item.Amount, cancellationToken);
        if (balance < movement.Amount)
            throw new InvalidOperationException("Este aporte ja foi utilizado por uma retirada ou transferencia.");
        if (!await database.Accounts.AnyAsync(item => item.Id == movement.AccountId, cancellationToken))
            throw new InvalidOperationException("A conta vinculada ao aporte nao foi encontrada.");

        database.ReserveMovements.Remove(movement);
        database.AccountBalanceCorrections.Add(new AccountBalanceCorrectionRecord
        {
            Id = Guid.NewGuid(), AccountId = movement.AccountId,
            Amount = movement.Amount, Date = movement.Date,
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReserveMovementItem>> GetReserveMovementsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        return await database.ReserveMovements.AsNoTracking()
            .OrderByDescending(item => item.Date)
            .Select(item => new ReserveMovementItem(
                item.Id, item.ReserveId, item.AccountId, item.Amount, item.IsDeposit, item.Date))
            .ToListAsync(cancellationToken);
    }

    public async Task TransferReserveAsync(TransferReserveRequest request, CancellationToken cancellationToken = default)
    {
        if (request.SourceReserveId == request.DestinationReserveId)
            throw new ArgumentException("Selecione reservas de origem e destino diferentes.");
        if (request.Amount <= 0m || request.Amount > MaxMonetaryAmount)
            throw new ArgumentException("Informe um valor valido para a transferencia.");
        await using var database = new LocalFinanceDbContext(_options);
        var reserveCount = await database.Reserves.CountAsync(item =>
            item.Id == request.SourceReserveId || item.Id == request.DestinationReserveId, cancellationToken);
        if (reserveCount != 2 || !await database.Accounts.AnyAsync(item => item.Id == request.AccountId, cancellationToken))
            throw new InvalidOperationException("Reserva ou conta nao encontrada.");
        var sourceBalance = await database.ReserveMovements.Where(item => item.ReserveId == request.SourceReserveId)
            .SumAsync(item => item.IsDeposit ? item.Amount : -item.Amount, cancellationToken);
        if (request.Amount > sourceBalance)
            throw new InvalidOperationException("A transferencia nao pode ser maior que o saldo da reserva de origem.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        database.ReserveMovements.AddRange(
            new ReserveMovementRecord
            {
                Id = Guid.NewGuid(), ReserveId = request.SourceReserveId, AccountId = request.AccountId,
                Amount = request.Amount, IsDeposit = false, Date = request.Date,
            },
            new ReserveMovementRecord
            {
                Id = Guid.NewGuid(), ReserveId = request.DestinationReserveId, AccountId = request.AccountId,
                Amount = request.Amount, IsDeposit = true, Date = request.Date,
            });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GoalItem>> GetGoalsAsync(CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        var movements = await database.ReserveMovements.AsNoTracking().ToListAsync(cancellationToken);
        var goals = await database.Goals.AsNoTracking().OrderBy(item => item.TargetDate).ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);
        return goals.Select(item =>
        {
            var reserveMovements = item.ReserveId is null
                ? new List<ReserveMovementRecord>()
                : movements.Where(movement => movement.ReserveId == item.ReserveId).ToList();
            var monthlyDeposits = reserveMovements.Where(movement => movement.IsDeposit)
                .GroupBy(movement => new { movement.Date.Year, movement.Date.Month })
                .Select(group => group.Sum(movement => movement.Amount)).ToList();
            return new GoalItem(item.Id, item.Name, item.TargetAmount, item.TargetDate,
                item.PlannedMonthlyContribution, item.ReserveId,
                reserveMovements.Sum(movement => movement.IsDeposit ? movement.Amount : -movement.Amount),
                monthlyDeposits.Count == 0 ? 0m : monthlyDeposits.Average(), item.IsPrimary);
        })
            .ToList();
    }

    public async Task AddGoalAsync(CreateGoalRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.TargetAmount <= 0m ||
            request.TargetAmount > MaxMonetaryAmount || request.PlannedMonthlyContribution < 0m ||
            request.PlannedMonthlyContribution > MaxMonetaryAmount)
            throw new ArgumentException("Informe nome, valor alvo e um aporte planejado valido.");
        await using var database = new LocalFinanceDbContext(_options);
        if (request.ReserveId is Guid reserveId &&
            !await database.Reserves.AnyAsync(item => item.Id == reserveId, cancellationToken))
            throw new InvalidOperationException("Reserva vinculada nao encontrada.");
        var isFirstGoal = !await database.Goals.AnyAsync(cancellationToken);
        database.Goals.Add(new GoalRecord
        {
            Id = Guid.NewGuid(), Name = request.Name.Trim(), TargetAmount = request.TargetAmount,
            TargetDate = request.TargetDate, PlannedMonthlyContribution = request.PlannedMonthlyContribution,
            ReserveId = request.ReserveId, IsPrimary = isFirstGoal,
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task SetPrimaryGoalAsync(Guid goalId, CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        var goals = await database.Goals.ToListAsync(cancellationToken);
        if (goals.All(item => item.Id != goalId))
            throw new InvalidOperationException("Objetivo nao encontrado.");

        foreach (var goal in goals)
            goal.IsPrimary = goal.Id == goalId;
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateGoalAsync(UpdateGoalRequest request, CancellationToken cancellationToken = default)
    {
        ValidateGoal(request.Name, request.TargetAmount, request.PlannedMonthlyContribution);
        await using var database = new LocalFinanceDbContext(_options);
        if (request.ReserveId is Guid reserveId &&
            !await database.Reserves.AnyAsync(item => item.Id == reserveId, cancellationToken))
            throw new InvalidOperationException("Reserva vinculada nao encontrada.");
        var goal = await database.Goals.SingleOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException("Objetivo nao encontrado.");
        goal.Name = request.Name.Trim(); goal.TargetAmount = request.TargetAmount;
        goal.TargetDate = request.TargetDate; goal.PlannedMonthlyContribution = request.PlannedMonthlyContribution;
        goal.ReserveId = request.ReserveId;
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteGoalAsync(Guid goalId, CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        var goal = await database.Goals.SingleOrDefaultAsync(item => item.Id == goalId, cancellationToken)
            ?? throw new InvalidOperationException("Objetivo nao encontrado.");
        database.Goals.Remove(goal);
        await database.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateGoal(string name, decimal targetAmount, decimal contribution)
    {
        if (string.IsNullOrWhiteSpace(name) || targetAmount <= 0m || targetAmount > MaxMonetaryAmount ||
            contribution < 0m || contribution > MaxMonetaryAmount)
            throw new ArgumentException("Informe nome, valor alvo e um aporte planejado valido.");
    }

    public async Task<IReadOnlyList<FinancialSpaceItem>> GetFinancialSpacesAsync(CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        return await database.FinancialSpaces.AsNoTracking()
            .OrderByDescending(item => item.IsPrimary).ThenBy(item => item.Name)
            .Select(item => new FinancialSpaceItem(item.Id, item.Name, item.IsPrimary))
            .ToListAsync(cancellationToken);
    }

    public async Task AddFinancialSpaceAsync(string name, string ownerName, CancellationToken cancellationToken = default)
    {
        name = name.Trim();
        ownerName = ownerName.Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Informe o nome do espaco.");
        if (string.IsNullOrWhiteSpace(ownerName)) throw new ArgumentException("Nao foi possivel identificar o responsavel pelo espaco.");
        await using var database = new LocalFinanceDbContext(_options);
        if (await database.FinancialSpaces.AnyAsync(item => item.Name.ToLower() == name.ToLower(), cancellationToken))
            throw new InvalidOperationException("Ja existe um espaco com este nome.");
        var spaceId = Guid.NewGuid();
        database.FinancialSpaces.Add(new FinancialSpaceRecord { Id = spaceId, Name = name, IsPrimary = false });
        database.Members.Add(new MemberRecord
        {
            Id = Guid.NewGuid(), SpaceId = spaceId, Name = ownerName, Email = $"{Guid.NewGuid():N}@local",
            Role = FinancialSpaceRole.Administrator, IsPrimary = true,
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task RenameFinancialSpaceAsync(Guid spaceId, string name, CancellationToken cancellationToken = default)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Informe o nome do espaco.");
        await using var database = new LocalFinanceDbContext(_options);
        var space = await database.FinancialSpaces.SingleOrDefaultAsync(item => item.Id == spaceId, cancellationToken)
            ?? throw new InvalidOperationException("Espaco nao encontrado.");
        if (await database.FinancialSpaces.AnyAsync(item => item.Id != spaceId && item.Name.ToLower() == name.ToLower(), cancellationToken))
            throw new InvalidOperationException("Ja existe um espaco com este nome.");
        space.Name = name;
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteFinancialSpaceAsync(Guid spaceId, Guid? replacementSpaceId = null, CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var space = await database.FinancialSpaces.SingleOrDefaultAsync(item => item.Id == spaceId, cancellationToken)
            ?? throw new InvalidOperationException("Espaco nao encontrado.");
        var replacement = await database.FinancialSpaces
            .Where(item => item.Id != spaceId && (replacementSpaceId == null || item.Id == replacementSpaceId))
            .OrderBy(item => item.Name)
            .FirstOrDefaultAsync(cancellationToken);
        if (replacement is null)
            throw new InvalidOperationException("Crie outro espaco antes de excluir o ultimo espaco.");
        if (space.IsPrimary) replacement.IsPrimary = true;
        database.Members.RemoveRange(await database.Members.Where(item => item.SpaceId == spaceId).ToListAsync(cancellationToken));
        database.FinancialSpaces.Remove(space);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LoginUserItem>> GetLoginUsersAsync(CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        var members = await database.Members.AsNoTracking().OrderBy(item => item.Name).ToListAsync(cancellationToken);
        return members.GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => new LoginUserItem(group.First().Name,
                group.Any(item => !string.IsNullOrWhiteSpace(item.PinHash) && !string.IsNullOrWhiteSpace(item.PinSalt))))
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task SetMemberPinAsync(string memberName, string pin, CancellationToken cancellationToken = default)
    {
        ValidatePin(pin);
        memberName = memberName.Trim();
        await using var database = new LocalFinanceDbContext(_options);
        var members = await database.Members
            .Where(item => item.Name.ToLower() == memberName.ToLower()).ToListAsync(cancellationToken);
        if (members.Count == 0) throw new InvalidOperationException("Participante nao encontrado.");
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = HashPin(pin, salt);
        foreach (var member in members)
        {
            member.PinSalt = Convert.ToBase64String(salt);
            member.PinHash = Convert.ToBase64String(hash);
        }
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> AuthenticateMemberAsync(string memberName, string pin, CancellationToken cancellationToken = default)
    {
        ValidatePin(pin);
        memberName = memberName.Trim();
        await using var database = new LocalFinanceDbContext(_options);
        var member = await database.Members.AsNoTracking().FirstOrDefaultAsync(item =>
            item.Name.ToLower() == memberName.ToLower() && item.PinSalt != null && item.PinHash != null,
            cancellationToken);
        if (member?.PinSalt is null || member.PinHash is null) return false;
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                HashPin(pin, Convert.FromBase64String(member.PinSalt)),
                Convert.FromBase64String(member.PinHash));
        }
        catch (FormatException) { return false; }
    }

    private static void ValidatePin(string pin)
    {
        if (pin.Length is < 4 or > 8 || pin.Any(character => !char.IsDigit(character)))
            throw new ArgumentException("O PIN deve ter entre 4 e 8 numeros.");
    }

    private static byte[] HashPin(string pin, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(pin, salt, 120_000, HashAlgorithmName.SHA256, 32);

    public async Task<IReadOnlyList<MemberItem>> GetMembersAsync(Guid spaceId, CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        return await database.Members.AsNoTracking().Where(item => item.SpaceId == spaceId)
            .OrderByDescending(item => item.IsPrimary).ThenBy(item => item.Name)
            .Select(item => new MemberItem(item.Id, item.SpaceId, item.Name, item.Role, item.IsPrimary))
            .ToListAsync(cancellationToken);
    }

    public async Task<SalarySettingsItem?> GetSalarySettingsAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        var member = await database.Members.AsNoTracking().SingleOrDefaultAsync(item => item.Id == memberId, cancellationToken);
        return member is { SalaryAmount: > 0, SalaryPayDay: >= 1 and <= 31, SalaryAccountId: Guid accountId }
            ? new SalarySettingsItem(memberId, member.SalaryAmount.Value, member.SalaryPayDay.Value, accountId)
            : null;
    }

    public async Task SaveSalarySettingsAsync(SaveSalarySettingsRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0 || request.Amount > MaxMonetaryAmount)
            throw new ArgumentException("Informe um salario maior que zero.");
        if (request.PayDay is < 1 or > 31)
            throw new ArgumentException("O dia do salario deve estar entre 1 e 31.");
        await using var database = new LocalFinanceDbContext(_options);
        var member = await database.Members.SingleOrDefaultAsync(item => item.Id == request.MemberId, cancellationToken)
            ?? throw new InvalidOperationException("Participante nao encontrado.");
        if (!await database.Accounts.AnyAsync(item => item.Id == request.AccountId, cancellationToken))
            throw new InvalidOperationException("Selecione uma conta existente para receber o salario.");
        member.SalaryAmount = request.Amount;
        member.SalaryPayDay = request.PayDay;
        member.SalaryAccountId = request.AccountId;
        member.SalaryStartDate ??= DateOnly.FromDateTime(DateTime.Today);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task PostDueSalariesAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        var members = await database.Members.AsNoTracking()
            .Where(item => item.SalaryAmount > 0 && item.SalaryPayDay != null && item.SalaryAccountId != null && item.SalaryStartDate != null)
            .ToListAsync(cancellationToken);
        if (members.Count == 0) return;
        var accountIds = await database.Accounts.AsNoTracking().Select(item => item.Id).ToListAsync(cancellationToken);
        foreach (var member in members)
        {
            if (!accountIds.Contains(member.SalaryAccountId!.Value)) continue;
            var month = new DateOnly(member.SalaryStartDate!.Value.Year, member.SalaryStartDate.Value.Month, 1);
            while (month <= today)
            {
                var paymentDate = new DateOnly(month.Year, month.Month,
                    Math.Min(member.SalaryPayDay!.Value, DateTime.DaysInMonth(month.Year, month.Month)));
                if (paymentDate >= member.SalaryStartDate.Value && paymentDate <= today)
                {
                    var key = $"salary:{member.Id:N}:{month:yyyyMM}";
                    var id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0, 16));
                    if (!await database.Transactions.AnyAsync(item => item.Id == id, cancellationToken))
                    {
                        if (!await database.Categories.AnyAsync(item => item.Id == SalaryCategoryId, cancellationToken))
                            database.Categories.Add(new CategoryRecord { Id = SalaryCategoryId, Name = "Salario", Type = TransactionType.Income });
                        database.Transactions.Add(new TransactionRecord
                        {
                            Id = id, AccountId = member.SalaryAccountId.Value, CategoryId = SalaryCategoryId,
                            Type = TransactionType.Income, Description = $"Salario de {member.Name}",
                            Amount = member.SalaryAmount!.Value, Date = paymentDate,
                            AuthorId = member.Id, AuthorName = member.Name, DeviceName = "Automatico",
                            CreatedAtUtc = DateTime.UtcNow, Version = 1,
                        });
                        await database.SaveChangesAsync(cancellationToken);
                    }
                }
                month = month.AddMonths(1);
            }
        }
    }

    public async Task AddMemberAsync(AddMemberRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name) ||
            request.Role is not (FinancialSpaceRole.Member or FinancialSpaceRole.Administrator))
            throw new ArgumentException("Informe um nome e um nivel de acesso validos.");
        await using var database = new LocalFinanceDbContext(_options);
        if (!await database.FinancialSpaces.AnyAsync(item => item.Id == request.SpaceId, cancellationToken))
            throw new InvalidOperationException("Espaco nao encontrado.");
        if (await database.Members.AnyAsync(item => item.SpaceId == request.SpaceId && item.Name.ToLower() == name.ToLower(), cancellationToken))
            throw new InvalidOperationException("Ja existe um participante com este nome no espaco.");
        database.Members.Add(new MemberRecord
        {
            Id = Guid.NewGuid(), SpaceId = request.SpaceId, Name = name, Email = $"{Guid.NewGuid():N}@local",
            Role = request.Role, IsPrimary = false,
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteMemberAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        await using var database = new LocalFinanceDbContext(_options);
        var member = await database.Members.SingleOrDefaultAsync(item => item.Id == memberId, cancellationToken)
            ?? throw new InvalidOperationException("Membro nao encontrado.");
        if (member.IsPrimary) throw new InvalidOperationException("O administrador principal nao pode ser removido.");
        database.Members.Remove(member);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateMemberRoleAsync(
        UpdateMemberRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Role is not (FinancialSpaceRole.Member or FinancialSpaceRole.Administrator))
            throw new ArgumentException("Informe um papel valido.");

        await using var database = new LocalFinanceDbContext(_options);
        var member = await database.Members.SingleOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException("Membro nao encontrado.");
        if (member.IsPrimary && request.Role != FinancialSpaceRole.Administrator)
            throw new InvalidOperationException("O administrador principal deve permanecer administrador.");

        member.Role = request.Role;
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<BackupItem> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        var backupDirectory = Path.Combine(Path.GetDirectoryName(_databasePath)!, "Backups");
        Directory.CreateDirectory(backupDirectory);
        var timestamp = DateTime.UtcNow;
        var backupPath = Path.Combine(backupDirectory, $"nickfinancas-{timestamp:yyyyMMdd-HHmmssfff}.db");
        // SQLite's online backup includes committed WAL frames and remains consistent during sync.
        // Copying the .db file alone can leave indexes and table pages from different moments.
        await using (var source = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_databasePath};Mode=ReadOnly;Pooling=False"))
        await using (var destination = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={backupPath};Pooling=False"))
        {
            await source.OpenAsync(cancellationToken);
            await destination.OpenAsync(cancellationToken);
            source.BackupDatabase(destination);
        }
        PruneBackups(backupDirectory, 10);
        var info = new FileInfo(backupPath);
        return new BackupItem(info.FullName, timestamp, info.Length);
    }

    public Task<IReadOnlyList<BackupItem>> GetBackupsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var backupDirectory = Path.Combine(Path.GetDirectoryName(_databasePath)!, "Backups");
        if (!Directory.Exists(backupDirectory)) return Task.FromResult<IReadOnlyList<BackupItem>>([]);
        IReadOnlyList<BackupItem> result = Directory.GetFiles(backupDirectory, "nickfinancas-*.db")
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.Name, StringComparer.Ordinal)
            .Select(info => new BackupItem(info.FullName, info.LastWriteTimeUtc, info.Length)).ToList();
        return Task.FromResult(result);
    }

    public async Task RestoreBackupAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        var fullBackupPath = Path.GetFullPath(backupPath);
        var backupDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(_databasePath)!, "Backups"));
        if (!fullBackupPath.StartsWith(backupDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(fullBackupPath))
            throw new InvalidOperationException("Arquivo de backup invalido.");
        await using (var source = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={fullBackupPath};Mode=ReadOnly;Pooling=False"))
        {
            await source.OpenAsync(cancellationToken);
            await using var command = source.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            if (!string.Equals(Convert.ToString(await command.ExecuteScalarAsync(cancellationToken)), "ok", StringComparison.Ordinal))
                throw new InvalidOperationException("O arquivo nao contem um banco valido do Nick FinanÃ§as.");
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'accounts';";
            if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) != 1)
                throw new InvalidOperationException("O arquivo nao contem um banco valido do Nick FinanÃ§as.");
            await using var destination = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_databasePath};Pooling=False");
            await destination.OpenAsync(cancellationToken);
            source.BackupDatabase(destination);
        }
        await InitializeAsync(cancellationToken);
    }

    public async Task<BackupMaintenanceResult> EnsureDailyBackupAsync(CancellationToken cancellationToken = default)
    {
        var backups = await GetBackupsAsync(cancellationToken);
        var hasTodayBackup = backups.Any(item => item.CreatedAtUtc.ToLocalTime().Date == DateTime.Today);
        if (!hasTodayBackup) await CreateBackupAsync(cancellationToken);
        var updated = await GetBackupsAsync(cancellationToken);
        return new BackupMaintenanceResult(!hasTodayBackup, updated.Count);
    }

    private static void PruneBackups(string backupDirectory, int keepCount)
    {
        foreach (var file in Directory.GetFiles(backupDirectory, "nickfinancas-*.db")
                     .Select(path => new FileInfo(path)).OrderByDescending(item => item.Name, StringComparer.Ordinal).Skip(keepCount))
            file.Delete();
    }

    private static async Task EnsureCreditCardSchemaAsync(
        LocalFinanceDbContext database,
        CancellationToken cancellationToken)
    {
        await database.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS credit_cards (
                Id TEXT NOT NULL CONSTRAINT PK_credit_cards PRIMARY KEY,
                Name TEXT NOT NULL,
                PaymentAccountId TEXT NOT NULL,
                CreditLimit TEXT NOT NULL,
                SpendingLimit TEXT NOT NULL,
                ClosingDay INTEGER NOT NULL,
                DueDay INTEGER NOT NULL,
                CONSTRAINT FK_credit_cards_accounts_PaymentAccountId
                    FOREIGN KEY (PaymentAccountId) REFERENCES accounts (Id) ON DELETE RESTRICT
            );
            CREATE INDEX IF NOT EXISTS IX_credit_cards_PaymentAccountId
                ON credit_cards (PaymentAccountId);
            """,
            cancellationToken);

        await database.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var checkCommand = database.Database.GetDbConnection().CreateCommand();
            checkCommand.CommandText =
                "SELECT COUNT(*) FROM pragma_table_info('credit_cards') WHERE name = 'SpendingLimit';";
            var columnExists = Convert.ToInt64(await checkCommand.ExecuteScalarAsync(cancellationToken)) > 0;
            if (!columnExists)
            {
                await using var alterCommand = database.Database.GetDbConnection().CreateCommand();
                alterCommand.CommandText = "ALTER TABLE credit_cards ADD COLUMN SpendingLimit TEXT NULL;";
                await alterCommand.ExecuteNonQueryAsync(cancellationToken);
                await database.Database.ExecuteSqlRawAsync(
                    "UPDATE credit_cards SET SpendingLimit = CreditLimit WHERE SpendingLimit IS NULL;",
                    cancellationToken);
            }
        }
        finally
        {
            await database.Database.CloseConnectionAsync();
        }
    }

    private static async Task EnsureTransactionSchemaAsync(
        LocalFinanceDbContext database,
        CancellationToken cancellationToken)
    {
        await database.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var columns = new Dictionary<string, string>
            {
                ["DestinationAccountId"] = "TEXT NULL",
                ["CreditCardId"] = "TEXT NULL",
                ["InvoiceYear"] = "INTEGER NULL",
                ["InvoiceMonth"] = "INTEGER NULL",
                ["DeviceName"] = "TEXT NOT NULL DEFAULT 'Dispositivo anterior'",
            };
            foreach (var column in columns)
            {
                await using var checkCommand = database.Database.GetDbConnection().CreateCommand();
                checkCommand.CommandText =
                    $"SELECT COUNT(*) FROM pragma_table_info('transactions') WHERE name = '{column.Key}';";
                var columnExists = Convert.ToInt64(await checkCommand.ExecuteScalarAsync(cancellationToken)) > 0;
                if (!columnExists)
                {
                    await using var alterCommand = database.Database.GetDbConnection().CreateCommand();
                    alterCommand.CommandText = $"ALTER TABLE transactions ADD COLUMN {column.Key} {column.Value};";
                    await alterCommand.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            await database.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS IX_transactions_DestinationAccountId ON transactions (DestinationAccountId);
                CREATE INDEX IF NOT EXISTS IX_transactions_CreditCardId ON transactions (CreditCardId);
                """,
                cancellationToken);
        }
        finally
        {
            await database.Database.CloseConnectionAsync();
        }
    }

    private static async Task EnsureCategorySchemaAsync(LocalFinanceDbContext database, CancellationToken cancellationToken)
    {
        await database.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var check = database.Database.GetDbConnection().CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('categories') WHERE name = 'ParentCategoryId';";
            if (Convert.ToInt64(await check.ExecuteScalarAsync(cancellationToken)) == 0)
            {
                await using var alter = database.Database.GetDbConnection().CreateCommand();
                alter.CommandText = "ALTER TABLE categories ADD COLUMN ParentCategoryId TEXT NULL;";
                await alter.ExecuteNonQueryAsync(cancellationToken);
            }
            await database.Database.ExecuteSqlRawAsync(
                "CREATE INDEX IF NOT EXISTS IX_categories_ParentCategoryId ON categories (ParentCategoryId);",
                cancellationToken);
        }
        finally { await database.Database.CloseConnectionAsync(); }
    }

    private static async Task EnsureFinancialAgendaSchemaAsync(
        LocalFinanceDbContext database,
        CancellationToken cancellationToken)
    {
        await database.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS recurring_payables (
                Id TEXT NOT NULL CONSTRAINT PK_recurring_payables PRIMARY KEY,
                Description TEXT NOT NULL,
                Amount TEXT NOT NULL,
                DueDay INTEGER NOT NULL,
                StartDate TEXT NOT NULL,
                EndDate TEXT NULL,
                AccountId TEXT NOT NULL,
                CategoryId TEXT NOT NULL,
                IsActive INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS payables (
                Id TEXT NOT NULL CONSTRAINT PK_payables PRIMARY KEY,
                Description TEXT NOT NULL,
                Amount TEXT NOT NULL,
                DueDate TEXT NOT NULL,
                AccountId TEXT NOT NULL,
                CategoryId TEXT NOT NULL,
                RecurringPayableId TEXT NULL,
                PaidTransactionId TEXT NULL,
                PaidDate TEXT NULL,
                IsDeleted INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS IX_payables_DueDate ON payables (DueDate);
            CREATE UNIQUE INDEX IF NOT EXISTS IX_payables_RecurringPayableId_DueDate
                ON payables (RecurringPayableId, DueDate);
            """,
            cancellationToken);
        await database.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var check = database.Database.GetDbConnection().CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('payables') WHERE name = 'IsDeleted';";
            if (Convert.ToInt64(await check.ExecuteScalarAsync(cancellationToken)) == 0)
            {
                await using var alter = database.Database.GetDbConnection().CreateCommand();
                alter.CommandText = "ALTER TABLE payables ADD COLUMN IsDeleted INTEGER NOT NULL DEFAULT 0;";
                await alter.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        finally { await database.Database.CloseConnectionAsync(); }
    }

    private static async Task EnsureInstallmentSchemaAsync(
        LocalFinanceDbContext database, CancellationToken cancellationToken)
    {
        await database.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            foreach (var table in new[] { "transactions", "payables" })
            {
                foreach (var (column, definition) in new[]
                {
                    ("InstallmentGroupId", "TEXT NULL"),
                    ("InstallmentNumber", "INTEGER NULL"),
                    ("InstallmentTotal", "INTEGER NULL"),
                })
                {
                    await using var check = database.Database.GetDbConnection().CreateCommand();
                    check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{column}';";
                    if (Convert.ToInt64(await check.ExecuteScalarAsync(cancellationToken)) != 0) continue;
                    await using var alter = database.Database.GetDbConnection().CreateCommand();
                    alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
                    await alter.ExecuteNonQueryAsync(cancellationToken);
                }
            }
        }
        finally { await database.Database.CloseConnectionAsync(); }
    }

    private static Task EnsureReserveSchemaAsync(LocalFinanceDbContext database, CancellationToken cancellationToken) =>
        database.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS reserves (
                Id TEXT NOT NULL CONSTRAINT PK_reserves PRIMARY KEY,
                Name TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS reserve_movements (
                Id TEXT NOT NULL CONSTRAINT PK_reserve_movements PRIMARY KEY,
                ReserveId TEXT NOT NULL,
                AccountId TEXT NOT NULL,
                Amount TEXT NOT NULL,
                IsDeposit INTEGER NOT NULL,
                Date TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_reserve_movements_ReserveId ON reserve_movements (ReserveId);
            CREATE TABLE IF NOT EXISTS account_balance_corrections (
                Id TEXT NOT NULL CONSTRAINT PK_account_balance_corrections PRIMARY KEY,
                AccountId TEXT NOT NULL,
                Amount TEXT NOT NULL,
                Date TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_account_balance_corrections_AccountId_Date
                ON account_balance_corrections (AccountId, Date);
            CREATE INDEX IF NOT EXISTS IX_reserve_movements_AccountId ON reserve_movements (AccountId);
            """, cancellationToken);

    private static async Task EnsureGoalSchemaAsync(LocalFinanceDbContext database, CancellationToken cancellationToken)
    {
        await database.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS goals (
                Id TEXT NOT NULL CONSTRAINT PK_goals PRIMARY KEY,
                Name TEXT NOT NULL,
                TargetAmount TEXT NOT NULL,
                TargetDate TEXT NULL,
                PlannedMonthlyContribution TEXT NOT NULL,
                ReserveId TEXT NULL,
                IsPrimary INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS IX_goals_ReserveId ON goals (ReserveId);
            """, cancellationToken);

        await database.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var check = database.Database.GetDbConnection().CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('goals') WHERE name = 'IsPrimary';";
            if (Convert.ToInt64(await check.ExecuteScalarAsync(cancellationToken)) == 0)
            {
                await using var alter = database.Database.GetDbConnection().CreateCommand();
                alter.CommandText = "ALTER TABLE goals ADD COLUMN IsPrimary INTEGER NOT NULL DEFAULT 0;";
                await alter.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        finally { await database.Database.CloseConnectionAsync(); }
    }

    private static async Task EnsureMemberSchemaAsync(LocalFinanceDbContext database, CancellationToken cancellationToken)
    {
        await database.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS financial_spaces (
                Id TEXT NOT NULL CONSTRAINT PK_financial_spaces PRIMARY KEY,
                Name TEXT NOT NULL,
                IsPrimary INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS members (
                Id TEXT NOT NULL CONSTRAINT PK_members PRIMARY KEY,
                SpaceId TEXT NOT NULL,
                Name TEXT NOT NULL,
                Email TEXT NOT NULL,
                Role INTEGER NOT NULL,
                IsPrimary INTEGER NOT NULL,
                PinSalt TEXT NULL,
                PinHash TEXT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_members_Email ON members (Email);
            """, cancellationToken);

        await database.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var check = database.Database.GetDbConnection().CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('members') WHERE name = 'SpaceId';";
            if (Convert.ToInt64(await check.ExecuteScalarAsync(cancellationToken)) == 0)
            {
                await using var alter = database.Database.GetDbConnection().CreateCommand();
                alter.CommandText = $"ALTER TABLE members ADD COLUMN SpaceId TEXT NOT NULL DEFAULT '{PrimaryFinancialSpaceId:D}';";
                await alter.ExecuteNonQueryAsync(cancellationToken);
            }
            foreach (var column in new Dictionary<string, string>
            {
                ["PinSalt"] = "TEXT NULL", ["PinHash"] = "TEXT NULL",
                ["SalaryAmount"] = "TEXT NULL", ["SalaryPayDay"] = "INTEGER NULL",
                ["SalaryAccountId"] = "TEXT NULL", ["SalaryStartDate"] = "TEXT NULL",
            })
            {
                await using var pinCheck = database.Database.GetDbConnection().CreateCommand();
                pinCheck.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('members') WHERE name = '{column.Key}';";
                if (Convert.ToInt64(await pinCheck.ExecuteScalarAsync(cancellationToken)) == 0)
                {
                    await using var pinAlter = database.Database.GetDbConnection().CreateCommand();
                    pinAlter.CommandText = $"ALTER TABLE members ADD COLUMN {column.Key} {column.Value};";
                    await pinAlter.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            await using var index = database.Database.GetDbConnection().CreateCommand();
            index.CommandText = "CREATE INDEX IF NOT EXISTS IX_members_SpaceId ON members (SpaceId);";
            await index.ExecuteNonQueryAsync(cancellationToken);
        }
        finally { await database.Database.CloseConnectionAsync(); }
    }

    private static async Task GenerateRecurringPayablesAsync(
        LocalFinanceDbContext database,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var rules = await database.RecurringPayables
            .Where(item => item.IsActive && item.StartDate <= monthEnd &&
                           (item.EndDate == null || item.EndDate >= monthStart))
            .ToListAsync(cancellationToken);
        foreach (var rule in rules)
        {
            var dueDate = SafeDate(year, month, rule.DueDay);
            var exists = await database.Payables.AnyAsync(
                item => item.RecurringPayableId == rule.Id && item.DueDate == dueDate,
                cancellationToken);
            if (!exists)
            {
                database.Payables.Add(new PayableRecord
                {
                    Id = Guid.NewGuid(), Description = rule.Description, Amount = rule.Amount,
                    DueDate = dueDate, AccountId = rule.AccountId, CategoryId = rule.CategoryId,
                    RecurringPayableId = rule.Id,
                });
            }
        }
        await database.SaveChangesAsync(cancellationToken);
    }

    private static void ValidatePayable(string description, decimal amount, Guid accountId, Guid categoryId)
    {
        if (string.IsNullOrWhiteSpace(description) || amount <= 0m || amount > MaxMonetaryAmount ||
            accountId == Guid.Empty || categoryId == Guid.Empty)
        {
            throw new ArgumentException("Descricao, valor, conta e categoria sao obrigatorios.");
        }
    }

    private static string FormatInstallmentDescription(string description, int? number, int? total) =>
        number is int n && total is int count ? $"{description} ({n}/{count})" : description;

    private static string BaseInstallmentDescription(string description, int? number, int? total)
    {
        if (number is not int installment || total is not int count) return description;
        var suffix = $" ({installment}/{count})";
        return description.EndsWith(suffix, StringComparison.Ordinal)
            ? description[..^suffix.Length] : description;
    }

    private static async Task EnsurePayableReferencesAsync(
        LocalFinanceDbContext database,
        Guid accountId,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        if (!await database.Accounts.AnyAsync(item => item.Id == accountId, cancellationToken) ||
            !await database.Categories.AnyAsync(
                item => item.Id == categoryId && item.Type == TransactionType.Expense,
                cancellationToken))
        {
            throw new InvalidOperationException("Conta ou categoria de despesa nao encontrada.");
        }
    }

    private static decimal CalculateAccountBalance(
        Guid accountId,
        decimal initialBalance,
        IEnumerable<TransactionRecord> transactions)
    {
        var balance = initialBalance;
        foreach (var transaction in transactions)
        {
            if (transaction.Type == TransactionType.Income && transaction.AccountId == accountId)
            {
                balance += transaction.Amount;
            }
            else if (transaction.Type == TransactionType.Expense &&
                     transaction.CreditCardId is null && transaction.AccountId == accountId)
            {
                balance -= transaction.Amount;
            }
            else if (transaction.Type == TransactionType.CreditCardPayment && transaction.AccountId == accountId)
            {
                balance -= transaction.Amount;
            }
            else if (transaction.Type == TransactionType.Transfer)
            {
                if (transaction.AccountId == accountId)
                {
                    balance -= transaction.Amount;
                }
                if (transaction.DestinationAccountId == accountId)
                {
                    balance += transaction.Amount;
                }
            }
        }

        return balance;
    }

    private static async Task<(Guid PaymentAccountId, int InvoiceYear, int InvoiceMonth)?> ResolveCardTransactionAsync(
        LocalFinanceDbContext database,
        TransactionType type,
        Guid? creditCardId,
        DateOnly purchaseDate,
        CancellationToken cancellationToken)
    {
        if (creditCardId is null)
        {
            return null;
        }
        if (type != TransactionType.Expense)
        {
            throw new InvalidOperationException("Somente despesas podem ser lancadas no cartao de credito.");
        }

        var card = await database.CreditCards.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == creditCardId, cancellationToken)
            ?? throw new InvalidOperationException("Cartao nao encontrado.");
        var closingReference = purchaseDate.Day <= card.ClosingDay ? purchaseDate : purchaseDate.AddMonths(1);
        var invoiceReference = card.DueDay > card.ClosingDay ? closingReference : closingReference.AddMonths(1);
        return (card.PaymentAccountId, invoiceReference.Year, invoiceReference.Month);
    }

    private static DateOnly SafeDate(int year, int month, int day) =>
        new(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));

    private static async Task EnsureTransferAccountsExistAsync(
        LocalFinanceDbContext database,
        TransactionType type,
        Guid sourceAccountId,
        Guid? destinationAccountId,
        CancellationToken cancellationToken)
    {
        if (type != TransactionType.Transfer)
        {
            return;
        }

        var accountIds = await database.Accounts
            .Where(account => account.Id == sourceAccountId || account.Id == destinationAccountId)
            .Select(account => account.Id)
            .ToListAsync(cancellationToken);
        if (!accountIds.Contains(sourceAccountId) || destinationAccountId is null || !accountIds.Contains(destinationAccountId.Value))
        {
            throw new InvalidOperationException("Uma das contas da transferencia nao foi encontrada.");
        }
    }

    private static void ValidateTransaction(
        Guid accountId,
        Guid categoryId,
        string description,
        decimal amount,
        TransactionType type,
        Guid? destinationAccountId)
    {
        if (accountId == Guid.Empty || (type != TransactionType.Transfer && categoryId == Guid.Empty))
        {
            throw new ArgumentException("Conta e categoria sao obrigatorias.");
        }

        if (amount <= 0 || amount > MaxMonetaryAmount)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "O valor deve estar entre R$ 0,01 e R$ 1 bilhao.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("A descricao e obrigatoria.", nameof(description));
        }

        if (type == TransactionType.Transfer &&
            (destinationAccountId is null || destinationAccountId == Guid.Empty || destinationAccountId == accountId))
        {
            throw new ArgumentException("Selecione contas de origem e destino diferentes.");
        }
    }
}
