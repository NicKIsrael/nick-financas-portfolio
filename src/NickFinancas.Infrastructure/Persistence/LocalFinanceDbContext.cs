using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using NickFinancas.Domain.Accounts;
using NickFinancas.Domain.Transactions;
using NickFinancas.Domain.Spaces;

namespace NickFinancas.Infrastructure.Persistence;

internal sealed class LocalFinanceDbContext(DbContextOptions<LocalFinanceDbContext> options) : DbContext(options)
{
    public bool SuppressSyncTracking { get; set; }

    public DbSet<AccountRecord> Accounts => Set<AccountRecord>();

    public DbSet<CategoryRecord> Categories => Set<CategoryRecord>();

    public DbSet<TransactionRecord> Transactions => Set<TransactionRecord>();

    public DbSet<CreditCardRecord> CreditCards => Set<CreditCardRecord>();

    public DbSet<PayableRecord> Payables => Set<PayableRecord>();

    public DbSet<RecurringPayableRecord> RecurringPayables => Set<RecurringPayableRecord>();
    public DbSet<ReserveRecord> Reserves => Set<ReserveRecord>();
    public DbSet<ReserveMovementRecord> ReserveMovements => Set<ReserveMovementRecord>();
    public DbSet<AccountBalanceCorrectionRecord> AccountBalanceCorrections => Set<AccountBalanceCorrectionRecord>();
    public DbSet<GoalRecord> Goals => Set<GoalRecord>();
    public DbSet<FinancialSpaceRecord> FinancialSpaces => Set<FinancialSpaceRecord>();
    public DbSet<MemberRecord> Members => Set<MemberRecord>();
    public DbSet<SyncOutboxRecord> SyncOutbox => Set<SyncOutboxRecord>();
    public DbSet<SyncStateRecord> SyncState => Set<SyncStateRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountRecord>(builder =>
        {
            builder.ToTable("accounts");
            builder.HasKey(account => account.Id);
            builder.Property(account => account.Name).HasMaxLength(120).IsRequired();
            builder.Property(account => account.InitialBalance).HasPrecision(18, 2);
        });

        modelBuilder.Entity<CategoryRecord>(builder =>
        {
            builder.ToTable("categories");
            builder.HasKey(category => category.Id);
            builder.Property(category => category.Name).HasMaxLength(80).IsRequired();
            builder.HasIndex(category => category.ParentCategoryId);
        });

        modelBuilder.Entity<TransactionRecord>(builder =>
        {
            builder.ToTable("transactions");
            builder.HasKey(transaction => transaction.Id);
            builder.Property(transaction => transaction.Description).HasMaxLength(180).IsRequired();
            builder.Property(transaction => transaction.Amount).HasPrecision(18, 2);
            builder.Property(transaction => transaction.AuthorName).HasMaxLength(120).IsRequired();
            builder.HasIndex(transaction => transaction.Date);
            builder.HasOne<AccountRecord>().WithMany().HasForeignKey(transaction => transaction.AccountId);
            builder.HasOne<CategoryRecord>().WithMany().HasForeignKey(transaction => transaction.CategoryId);
            builder.HasIndex(transaction => transaction.DestinationAccountId);
            builder.HasIndex(transaction => transaction.CreditCardId);
        });

        modelBuilder.Entity<CreditCardRecord>(builder =>
        {
            builder.ToTable("credit_cards");
            builder.HasKey(card => card.Id);
            builder.Property(card => card.Name).HasMaxLength(120).IsRequired();
            builder.Property(card => card.CreditLimit).HasPrecision(18, 2);
            builder.Property(card => card.SpendingLimit).HasPrecision(18, 2);
            builder.HasOne<AccountRecord>().WithMany().HasForeignKey(card => card.PaymentAccountId);
        });

        modelBuilder.Entity<PayableRecord>(builder =>
        {
            builder.ToTable("payables");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Description).HasMaxLength(180).IsRequired();
            builder.Property(item => item.Amount).HasPrecision(18, 2);
            builder.HasIndex(item => item.DueDate);
            builder.HasIndex(item => new { item.RecurringPayableId, item.DueDate }).IsUnique();
        });

        modelBuilder.Entity<RecurringPayableRecord>(builder =>
        {
            builder.ToTable("recurring_payables");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Description).HasMaxLength(180).IsRequired();
            builder.Property(item => item.Amount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<ReserveRecord>(builder =>
        {
            builder.ToTable("reserves");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Name).HasMaxLength(120).IsRequired();
        });

        modelBuilder.Entity<ReserveMovementRecord>(builder =>
        {
            builder.ToTable("reserve_movements");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Amount).HasPrecision(18, 2);
            builder.HasIndex(item => item.ReserveId);
            builder.HasIndex(item => item.AccountId);
        });

        modelBuilder.Entity<AccountBalanceCorrectionRecord>(builder =>
        {
            builder.ToTable("account_balance_corrections");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Amount).HasPrecision(18, 2);
            builder.HasIndex(item => new { item.AccountId, item.Date });
        });

        modelBuilder.Entity<GoalRecord>(builder =>
        {
            builder.ToTable("goals");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Name).HasMaxLength(160).IsRequired();
            builder.Property(item => item.TargetAmount).HasPrecision(18, 2);
            builder.Property(item => item.PlannedMonthlyContribution).HasPrecision(18, 2);
            builder.HasIndex(item => item.ReserveId);
        });

        modelBuilder.Entity<FinancialSpaceRecord>(builder =>
        {
            builder.ToTable("financial_spaces");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Name).HasMaxLength(120).IsRequired();
        });

        modelBuilder.Entity<MemberRecord>(builder =>
        {
            builder.ToTable("members");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Name).HasMaxLength(120).IsRequired();
            builder.Property(item => item.Email).HasMaxLength(180).IsRequired();
            builder.HasIndex(item => item.Email).IsUnique();
            builder.HasIndex(item => item.SpaceId);
        });

        modelBuilder.Entity<SyncOutboxRecord>(builder =>
        {
            builder.ToTable("sync_outbox");
            builder.HasKey(item => item.ChangeId);
            builder.Property(item => item.EntityType).HasMaxLength(100).IsRequired();
            builder.Property(item => item.Operation).HasMaxLength(20).IsRequired();
            builder.Property(item => item.Payload).IsRequired();
            builder.HasIndex(item => item.CreatedAtUtc);
        });

        modelBuilder.Entity<SyncStateRecord>(builder =>
        {
            builder.ToTable("sync_state");
            builder.HasKey(item => item.Id);
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var hasLocalChanges = false;
        if (!SuppressSyncTracking)
        {
            ChangeTracker.DetectChanges();
            var changes = ChangeTracker.Entries()
                .Where(entry => entry.Entity is not SyncOutboxRecord and not SyncStateRecord &&
                                entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .ToList();
            hasLocalChanges = changes.Count > 0;

            foreach (var entry in changes)
            {
                var idProperty = entry.Metadata.FindProperty("Id");
                var idValue = entry.State == EntityState.Deleted
                    ? idProperty is null ? null : entry.Property(idProperty.Name).OriginalValue
                    : idProperty is null ? null : entry.Property(idProperty.Name).CurrentValue;
                if (idValue is not Guid entityId || entityId == Guid.Empty)
                    continue;

                SyncOutbox.Add(new SyncOutboxRecord
                {
                    ChangeId = Guid.NewGuid(),
                    EntityType = entry.Metadata.ClrType.Name,
                    EntityId = entityId,
                    Operation = entry.State == EntityState.Deleted ? "delete" : "upsert",
                    Payload = entry.State == EntityState.Deleted
                        ? "{}"
                        : JsonSerializer.Serialize(entry.Entity, entry.Metadata.ClrType),
                    ChangedAtUtc = DateTimeOffset.UtcNow,
                    LocalVersion = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                });
            }
        }

        var result = await base.SaveChangesAsync(cancellationToken);
        if (hasLocalChanges) SqliteFinanceStore.NotifyLocalDataChanged();
        return result;
    }
}

internal sealed class SyncOutboxRecord
{
    public Guid ChangeId { get; set; }
    public required string EntityType { get; set; }
    public Guid EntityId { get; set; }
    public required string Operation { get; set; }
    public required string Payload { get; set; }
    public DateTimeOffset ChangedAtUtc { get; set; }
    public long LocalVersion { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class SyncStateRecord
{
    public int Id { get; set; }
    public long LastPulledSequence { get; set; }
    public DateTimeOffset? LastSuccessfulSyncUtc { get; set; }
}

internal sealed class AccountRecord
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public FinancialAccountType Type { get; set; }
    public decimal InitialBalance { get; set; }
}

internal sealed class CategoryRecord
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public TransactionType Type { get; set; }
    public Guid? ParentCategoryId { get; set; }
}

internal sealed class TransactionRecord
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid? DestinationAccountId { get; set; }
    public Guid? CreditCardId { get; set; }
    public int? InvoiceYear { get; set; }
    public int? InvoiceMonth { get; set; }
    public Guid? InstallmentGroupId { get; set; }
    public int? InstallmentNumber { get; set; }
    public int? InstallmentTotal { get; set; }
    public TransactionType Type { get; set; }
    public required string Description { get; set; }
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public Guid AuthorId { get; set; }
    public required string AuthorName { get; set; }
    public required string DeviceName { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public long Version { get; set; }
    public bool IsDeleted { get; set; }
}

internal sealed class CreditCardRecord
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public Guid PaymentAccountId { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal SpendingLimit { get; set; }
    public int ClosingDay { get; set; }
    public int DueDay { get; set; }
}

internal sealed class PayableRecord
{
    public Guid Id { get; set; }
    public bool IsDeleted { get; set; }
    public required string Description { get; set; }
    public decimal Amount { get; set; }
    public DateOnly DueDate { get; set; }
    public Guid AccountId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid? RecurringPayableId { get; set; }
    public Guid? PaidTransactionId { get; set; }
    public DateOnly? PaidDate { get; set; }
    public Guid? InstallmentGroupId { get; set; }
    public int? InstallmentNumber { get; set; }
    public int? InstallmentTotal { get; set; }
}

internal sealed class RecurringPayableRecord
{
    public Guid Id { get; set; }
    public required string Description { get; set; }
    public decimal Amount { get; set; }
    public int DueDay { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public Guid AccountId { get; set; }
    public Guid CategoryId { get; set; }
    public bool IsActive { get; set; }
}

internal sealed class ReserveRecord
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
}

internal sealed class ReserveMovementRecord
{
    public Guid Id { get; set; }
    public Guid ReserveId { get; set; }
    public Guid AccountId { get; set; }
    public decimal Amount { get; set; }
    public bool IsDeposit { get; set; }
    public DateOnly Date { get; set; }
}

internal sealed class AccountBalanceCorrectionRecord
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
}

internal sealed class GoalRecord
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public decimal TargetAmount { get; set; }
    public DateOnly? TargetDate { get; set; }
    public decimal PlannedMonthlyContribution { get; set; }
    public Guid? ReserveId { get; set; }
    public bool IsPrimary { get; set; }
}

internal sealed class MemberRecord
{
    public Guid Id { get; set; }
    public Guid SpaceId { get; set; } = Guid.Parse("1a6580fa-9309-4f4d-a7a4-082cc757252b");
    public required string Name { get; set; }
    public required string Email { get; set; }
    public FinancialSpaceRole Role { get; set; }
    public bool IsPrimary { get; set; }
    public string? PinSalt { get; set; }
    public string? PinHash { get; set; }
    public decimal? SalaryAmount { get; set; }
    public int? SalaryPayDay { get; set; }
    public Guid? SalaryAccountId { get; set; }
    public DateOnly? SalaryStartDate { get; set; }
}

internal sealed class FinancialSpaceRecord
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public bool IsPrimary { get; set; }
}
