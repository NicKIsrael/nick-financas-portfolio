using Microsoft.Data.Sqlite;
using NickFinancas.Application.Finance;
using NickFinancas.Domain.Accounts;
using NickFinancas.Domain.Transactions;
using NickFinancas.Domain.Spaces;
using NickFinancas.Infrastructure.Persistence;

namespace NickFinancas.Infrastructure.Tests.Persistence;

public sealed class SqliteFinanceStoreTests
{
    [Fact]
    public async Task SalaryPostsOnceWhenDueAndIsNotRecreatedAfterDeletion()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath, seedSampleData: false);
            await store.InitializeAsync();
            await store.AddAccountAsync(new CreateAccountRequest("Conta salario", FinancialAccountType.Checking, 0));
            var account = Assert.Single((await store.GetDashboardAsync(2026, 10)).Accounts);
            var member = Assert.Single(await store.GetMembersAsync(
                Assert.Single(await store.GetFinancialSpacesAsync()).Id));
            var today = DateOnly.FromDateTime(DateTime.Today);
            await store.SaveSalarySettingsAsync(new SaveSalarySettingsRequest(member.Id, 2500m, today.Day, account.Id));

            await store.PostDueSalariesAsync(today.AddDays(-1));
            Assert.Empty(await store.GetTransactionsAsync(today.Year, today.Month));
            await store.PostDueSalariesAsync(today);
            await store.PostDueSalariesAsync(today);
            var salary = Assert.Single(await store.GetTransactionsAsync(today.Year, today.Month));
            Assert.Equal(TransactionType.Income, salary.Type);
            Assert.Equal(2500m, salary.Amount);
            Assert.Equal(today, salary.Date);
            Assert.Equal(2500m, (await store.GetDashboardAsync(today.Year, today.Month)).MonthlyIncome);

            await store.DeleteTransactionAsync(salary.Id);
            await store.PostDueSalariesAsync(today);
            Assert.Empty(await store.GetTransactionsAsync(today.Year, today.Month));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task ExistingMemberTableGainsSalarySettingsColumns()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath, seedSampleData: false);
            await store.InitializeAsync();
            await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                await connection.OpenAsync();
                foreach (var column in new[] { "SalaryAmount", "SalaryPayDay", "SalaryAccountId", "SalaryStartDate" })
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = $"ALTER TABLE members DROP COLUMN {column};";
                    await command.ExecuteNonQueryAsync();
                }
            }

            await store.InitializeAsync();
            await store.AddAccountAsync(new CreateAccountRequest("Conta", FinancialAccountType.Checking, 0));
            var account = Assert.Single((await store.GetDashboardAsync(2026, 10)).Accounts);
            var member = Assert.Single(await store.GetMembersAsync(
                Assert.Single(await store.GetFinancialSpacesAsync()).Id));
            await store.SaveSalarySettingsAsync(new SaveSalarySettingsRequest(member.Id, 3000m, 5, account.Id));
            Assert.Equal(3000m, (await store.GetSalarySettingsAsync(member.Id))?.Amount);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task ProductionDatabaseStartsWithoutSampleFinancialData()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath, seedSampleData: false);
            await store.InitializeAsync();
            var dashboard = await store.GetDashboardAsync(2026, 9);

            Assert.Empty(dashboard.Accounts);
            Assert.Empty(dashboard.Categories);
            Assert.Empty(dashboard.RecentTransactions);
            Assert.Equal(0m, dashboard.AvailableBalance);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task ExistingDatabaseGainsInstallmentColumnsWithoutLosingTransactions()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            var dashboard = await store.GetDashboardAsync(2026, 9);
            var account = dashboard.Accounts[0];
            var category = dashboard.Categories.First(item => item.Type == TransactionType.Expense);
            await store.AddTransactionAsync(new CreateTransactionRequest(
                account.Id, category.Id, TransactionType.Expense, "Compra antiga", 20m,
                new DateOnly(2026, 9, 1), Guid.NewGuid(), "Alex"));

            await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                await connection.OpenAsync();
                foreach (var table in new[] { "transactions", "payables" })
                foreach (var column in new[] { "InstallmentGroupId", "InstallmentNumber", "InstallmentTotal" })
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = $"ALTER TABLE {table} DROP COLUMN {column};";
                    await command.ExecuteNonQueryAsync();
                }
            }

            await store.InitializeAsync();
            Assert.Equal("Compra antiga", Assert.Single(await store.GetTransactionsAsync(2026, 9)).Description);
            Assert.Empty(await store.GetInstallmentPlansAsync());
            await store.AddPayableAsync(new CreatePayableRequest(
                "Compra nova", 10m, new DateOnly(2026, 10, 1), account.Id, category.Id, 2));
            Assert.Equal(2, Assert.Single(await store.GetInstallmentPlansAsync()).RemainingCount);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task ExistingDatabaseGainsPayableDeletionColumnWithoutLosingPendingItems()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            var dashboard = await store.GetDashboardAsync(2026, 10);
            await store.AddPayableAsync(new CreatePayableRequest(
                "Academia", 129.90m, new DateOnly(2026, 10, 5), dashboard.Accounts[0].Id,
                dashboard.Categories.First(item => item.Type == TransactionType.Expense).Id));
            await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "ALTER TABLE payables DROP COLUMN IsDeleted;";
                await command.ExecuteNonQueryAsync();
            }

            await store.InitializeAsync();
            var payable = Assert.Single(await store.GetPayablesAsync(2026, 10));
            Assert.Equal("Academia", payable.Description);
            await store.DeletePayableAsync(payable.Id);
            Assert.Empty(await store.GetPayablesAsync(2026, 10));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task CategoryDeletionNamesPayablesInOtherMonthsAndSucceedsAfterRemoval()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            var dashboard = await store.GetDashboardAsync(2026, 10);
            var account = dashboard.Accounts.First();
            await store.AddCategoryAsync(new CreateCategoryRequest("Alimento", TransactionType.Expense));
            var category = (await store.GetDashboardAsync(2026, 10)).Categories
                .Single(item => item.Name == "Alimento");
            await store.AddPayableAsync(new CreatePayableRequest(
                "Compra futura", 40m, new DateOnly(2026, 11, 15), account.Id, category.Id));

            Assert.Empty(await store.GetPayablesAsync(2026, 10));
            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => store.DeleteCategoryAsync(category.Id));
            Assert.Contains("1 pendencia(s)", error.Message);
            Assert.Contains("15/11/2026", error.Message);

            var payable = Assert.Single(await store.GetPayablesAsync(2026, 11));
            await store.DeletePayableAsync(payable.Id);
            await store.DeleteCategoryAsync(category.Id);
            Assert.DoesNotContain((await store.GetDashboardAsync(2026, 10)).Categories,
                item => item.Id == category.Id);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task AccountAndCategoryBecomeAvailableForTransactions()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");

        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();

            await store.AddAccountAsync(new CreateAccountRequest("Banco de teste", FinancialAccountType.Checking, 500m));
            await store.AddCategoryAsync(new CreateCategoryRequest("Transporte", TransactionType.Expense));

            var dashboard = await store.GetDashboardAsync(2026, 9);

            Assert.Contains(dashboard.Accounts, account => account.Name == "Banco de teste");
            Assert.Contains(dashboard.Categories, category => category.Name == "Transporte");
            Assert.Equal(6980.20m, dashboard.AvailableBalance);

            var accountId = dashboard.Accounts.Single(account => account.Name == "Banco de teste").Id;
            var categoryId = dashboard.Categories.Single(category => category.Name == "Transporte").Id;
            await store.DeleteAccountAsync(accountId);
            await store.DeleteCategoryAsync(categoryId);

            var afterDeletion = await store.GetDashboardAsync(2026, 9);
            Assert.DoesNotContain(afterDeletion.Accounts, account => account.Id == accountId);
            Assert.DoesNotContain(afterDeletion.Categories, category => category.Id == categoryId);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task TransactionPersistsWhenStoreIsRecreated()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");

        try
        {
            var firstStore = new SqliteFinanceStore(databasePath);
            await firstStore.InitializeAsync();
            var initial = await firstStore.GetDashboardAsync(2026, 9);
            var expenseCategory = initial.Categories.First(category => category.Type == TransactionType.Expense);

            await firstStore.AddTransactionAsync(new CreateTransactionRequest(
                initial.Accounts[0].Id,
                expenseCategory.Id,
                TransactionType.Expense,
                "Compra de teste",
                125.90m,
                new DateOnly(2026, 9, 15),
                Guid.NewGuid(),
                "Alex"));

            var reopenedStore = new SqliteFinanceStore(databasePath);
            await reopenedStore.InitializeAsync();
            var reopened = await reopenedStore.GetDashboardAsync(2026, 9);

            Assert.Equal(125.90m, reopened.MonthlyExpense);
            var persisted = Assert.Single(reopened.RecentTransactions, transaction => transaction.Description == "Compra de teste");
            Assert.Equal("Alex", persisted.AuthorName);
            Assert.False(string.IsNullOrWhiteSpace(persisted.DeviceName));
            await Assert.ThrowsAsync<InvalidOperationException>(() => reopenedStore.DeleteAccountAsync(initial.Accounts[0].Id));
            var categoryError = await Assert.ThrowsAsync<InvalidOperationException>(
                () => reopenedStore.DeleteCategoryAsync(expenseCategory.Id));
            Assert.Contains("15/09/2026", categoryError.Message);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task CreditCardPersistsAndProtectsItsPaymentAccount()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");

        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            await store.AddAccountAsync(new CreateAccountRequest("Conta do cartao", FinancialAccountType.Checking, 0m));
            var dashboard = await store.GetDashboardAsync(2026, 9);
            var account = dashboard.Accounts.Single(item => item.Name == "Conta do cartao");

            await store.AddCreditCardAsync(new CreateCreditCardRequest("Cartao de teste", account.Id, 3500m, 10, 17));

            var reopenedStore = new SqliteFinanceStore(databasePath);
            await reopenedStore.InitializeAsync();
            var card = Assert.Single(await reopenedStore.GetCreditCardsAsync());
            Assert.Equal("Cartao de teste", card.Name);
            Assert.Equal(3500m, card.CreditLimit);
            Assert.Equal(3500m, card.SpendingLimit);
            Assert.Equal(10, card.ClosingDay);
            Assert.Equal(17, card.DueDay);
            await Assert.ThrowsAsync<InvalidOperationException>(() => reopenedStore.DeleteAccountAsync(account.Id));

            await reopenedStore.DeleteCreditCardAsync(card.Id);
            Assert.Empty(await reopenedStore.GetCreditCardsAsync());
            await reopenedStore.DeleteAccountAsync(account.Id);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task TransactionCanBeListedUpdatedAndSoftDeleted()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");

        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            var dashboard = await store.GetDashboardAsync(2026, 9);
            var account = dashboard.Accounts[0];
            var expenseCategory = dashboard.Categories.First(item => item.Type == TransactionType.Expense);
            var incomeCategory = dashboard.Categories.First(item => item.Type == TransactionType.Income);

            await store.AddTransactionAsync(new CreateTransactionRequest(
                account.Id, expenseCategory.Id, TransactionType.Expense, "Valor original", 50m,
                new DateOnly(2026, 9, 10), Guid.NewGuid(), "Alex"));

            var transaction = Assert.Single(await store.GetTransactionsAsync(2026, 9));
            await store.UpdateTransactionAsync(new UpdateTransactionRequest(
                transaction.Id, account.Id, incomeCategory.Id, TransactionType.Income,
                "Valor atualizado", 75m, new DateOnly(2026, 9, 11)));

            var updated = Assert.Single(await store.GetTransactionsAsync(2026, 9));
            Assert.Equal("Valor atualizado", updated.Description);
            Assert.Equal(75m, updated.Amount);
            Assert.Equal(TransactionType.Income, updated.Type);

            await store.DeleteTransactionAsync(updated.Id);

            Assert.Empty(await store.GetTransactionsAsync(2026, 9));
            var afterDeletion = await store.GetDashboardAsync(2026, 9);
            Assert.Equal(0m, afterDeletion.MonthlyIncome);
            Assert.Equal(0m, afterDeletion.MonthlyExpense);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task CategoryUsedOnlyByDeletedTransactionsCanBeRemoved()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            await store.AddCategoryAsync(new CreateCategoryRequest("Temporaria", TransactionType.Expense));
            var dashboard = await store.GetDashboardAsync(2026, 9);
            var category = dashboard.Categories.Single(item => item.Name == "Temporaria");
            await store.AddTransactionAsync(new CreateTransactionRequest(
                dashboard.Accounts[0].Id, category.Id, TransactionType.Expense, "Compra apagada", 10m,
                new DateOnly(2026, 9, 10), Guid.NewGuid(), "Alex"));
            var transaction = Assert.Single(await store.GetTransactionsAsync(2026, 9));
            await store.DeleteTransactionAsync(transaction.Id);

            await store.DeleteCategoryAsync(category.Id);

            Assert.DoesNotContain((await store.GetDashboardAsync(2026, 9)).Categories,
                item => item.Id == category.Id);
            await using var connection = new SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync();
            await using var count = connection.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM transactions WHERE Id = $id";
            count.Parameters.AddWithValue("$id", transaction.Id.ToString().ToUpperInvariant());
            Assert.Equal(0L, await count.ExecuteScalarAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task AccountUsedOnlyByDeletedTransactionsCanBeRemoved()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            await store.AddAccountAsync(new CreateAccountRequest("Temporaria", FinancialAccountType.Checking, 0m));
            var dashboard = await store.GetDashboardAsync(2026, 9);
            var account = dashboard.Accounts.Single(item => item.Name == "Temporaria");
            var category = dashboard.Categories.First(item => item.Type == TransactionType.Expense);
            await store.AddTransactionAsync(new CreateTransactionRequest(
                account.Id, category.Id, TransactionType.Expense, "Compra apagada", 10m,
                new DateOnly(2026, 9, 10), Guid.NewGuid(), "Alex"));
            var transaction = Assert.Single(await store.GetTransactionsAsync(2026, 9));
            await store.DeleteTransactionAsync(transaction.Id);

            await store.DeleteAccountAsync(account.Id);

            Assert.DoesNotContain((await store.GetDashboardAsync(2026, 9)).Accounts,
                item => item.Id == account.Id);
            Assert.Empty(await store.GetTransactionsAsync(2026, 9));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task HistoricalBalanceIncludesPriorMonthsAndTransferKeepsConsolidatedTotal()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");

        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            var initial = await store.GetDashboardAsync(2026, 7);
            var source = initial.Accounts.Single(item => item.Name == "Conta conjunta");
            var destination = initial.Accounts.Single(item => item.Name == "Carteira");
            var incomeCategory = initial.Categories.First(item => item.Type == TransactionType.Income);

            await store.AddTransactionAsync(new CreateTransactionRequest(
                source.Id, incomeCategory.Id, TransactionType.Income, "Receita de julho", 1000m,
                new DateOnly(2026, 7, 20), Guid.NewGuid(), "Alex"));
            await store.AddTransactionAsync(new CreateTransactionRequest(
                source.Id, Guid.Empty, TransactionType.Transfer, "Dinheiro para carteira", 300m,
                new DateOnly(2026, 8, 5), Guid.NewGuid(), "Alex", destination.Id));

            var august = await store.GetDashboardAsync(2026, 8);

            Assert.Equal(7480.20m, august.AvailableBalance);
            Assert.Equal(7180.20m, august.Accounts.Single(item => item.Id == source.Id).CurrentBalance);
            Assert.Equal(300m, august.Accounts.Single(item => item.Id == destination.Id).CurrentBalance);
            Assert.Equal(0m, august.MonthlyIncome);
            Assert.Equal(0m, august.MonthlyExpense);
            var transfer = Assert.Single(await store.GetTransactionsAsync(2026, 8));
            Assert.Equal(TransactionType.Transfer, transfer.Type);
            Assert.Equal(destination.Id, transfer.DestinationAccountId);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task CreditCardPurchaseBuildsInvoiceAndPaymentDoesNotDuplicateExpense()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");

        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            var initial = await store.GetDashboardAsync(2026, 9);
            var account = initial.Accounts.Single(item => item.Name == "Conta conjunta");
            var category = initial.Categories.First(item => item.Type == TransactionType.Expense);
            await store.AddCreditCardAsync(new CreateCreditCardRequest("Cartao principal", account.Id, 1000m, 10, 17));
            var card = Assert.Single(await store.GetCreditCardsAsync());

            await store.AddTransactionAsync(new CreateTransactionRequest(
                account.Id, category.Id, TransactionType.Expense, "Compra no cartao", 100m,
                new DateOnly(2026, 9, 5), Guid.NewGuid(), "Alex", CreditCardId: card.Id));

            var beforePayment = await store.GetDashboardAsync(2026, 9);
            Assert.Equal(100m, beforePayment.MonthlyExpense);
            Assert.Equal(6480.20m, beforePayment.AvailableBalance);
            var openInvoice = Assert.Single(await store.GetCreditCardInvoicesAsync(2026, 9));
            Assert.Equal(100m, openInvoice.Total);
            Assert.Equal(100m, openInvoice.Outstanding);
            Assert.Equal(100m, openInvoice.UsedLimit);
            Assert.Equal(900m, openInvoice.AvailableLimit);

            await store.PayCreditCardInvoiceAsync(new PayCreditCardInvoiceRequest(
                card.Id, 2026, 9, new DateOnly(2026, 9, 17), Guid.NewGuid(), "Alex"));

            var afterPayment = await store.GetDashboardAsync(2026, 9);
            Assert.Equal(100m, afterPayment.MonthlyExpense);
            Assert.Equal(6380.20m, afterPayment.AvailableBalance);
            var paidInvoice = Assert.Single(await store.GetCreditCardInvoicesAsync(2026, 9));
            Assert.Equal(0m, paidInvoice.Outstanding);
            Assert.Equal(0m, paidInvoice.UsedLimit);
            Assert.Equal("Paga", paidInvoice.Status);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task CreditCardLimitCanBeAdjustedAndKeepsUsedAmount()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");

        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            var initial = await store.GetDashboardAsync(2026, 9);
            var account = initial.Accounts.Single(item => item.Name == "Conta conjunta");
            var category = initial.Categories.First(item => item.Type == TransactionType.Expense);
            await store.AddCreditCardAsync(new CreateCreditCardRequest("Cartao ajustavel", account.Id, 1000m, 10, 17));
            var card = Assert.Single(await store.GetCreditCardsAsync());
            await store.AddTransactionAsync(new CreateTransactionRequest(
                account.Id, category.Id, TransactionType.Expense, "Compra no cartao", 100m,
                new DateOnly(2026, 9, 5), Guid.NewGuid(), "Alex", CreditCardId: card.Id));

            await store.UpdateCreditCardLimitAsync(new UpdateCreditCardLimitRequest(card.Id, 500m));

            var updatedCard = Assert.Single(await store.GetCreditCardsAsync());
            var invoice = Assert.Single(await store.GetCreditCardInvoicesAsync(2026, 9));
            Assert.Equal(1000m, updatedCard.CreditLimit);
            Assert.Equal(500m, updatedCard.SpendingLimit);
            Assert.Equal(500m, invoice.CreditLimit);
            Assert.Equal(1000m, invoice.MaximumLimit);
            Assert.Equal(100m, invoice.UsedLimit);
            Assert.Equal(400m, invoice.AvailableLimit);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task CreditCardDetailsCanBeEdited()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");

        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            var dashboard = await store.GetDashboardAsync(2026, 9);
            var firstAccount = dashboard.Accounts[0];
            var secondAccount = dashboard.Accounts[1];
            await store.AddCreditCardAsync(new CreateCreditCardRequest(
                "Cartao original", firstAccount.Id, 1000m, 10, 17));
            var card = Assert.Single(await store.GetCreditCardsAsync());

            await store.UpdateCreditCardAsync(new UpdateCreditCardRequest(
                card.Id, "Cartao editado", secondAccount.Id, 1500m, 12, 20));

            var updated = Assert.Single(await store.GetCreditCardsAsync());
            Assert.Equal("Cartao editado", updated.Name);
            Assert.Equal(secondAccount.Id, updated.PaymentAccountId);
            Assert.Equal(1500m, updated.CreditLimit);
            Assert.Equal(12, updated.ClosingDay);
            Assert.Equal(20, updated.DueDay);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task DeletingPaidPayableAlsoRemovesItsExpenseFromBalanceAndReport()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            var initial = await store.GetDashboardAsync(2026, 10);
            var account = initial.Accounts.First();
            var category = initial.Categories.First(item => item.Type == TransactionType.Expense);
            await store.AddPayableAsync(new CreatePayableRequest(
                "Aluguel", 502m, new DateOnly(2026, 10, 30), account.Id, category.Id));
            var payable = Assert.Single(await store.GetPayablesAsync(2026, 10));
            await store.PayPayableAsync(new PayPayableRequest(
                payable.Id, new DateOnly(2026, 10, 3), Guid.NewGuid(), "Alex"));
            Assert.Equal(502m, (await store.GetDashboardAsync(2026, 10)).MonthlyExpense);

            await store.DeletePayableAsync(payable.Id);

            Assert.Empty(await store.GetPayablesAsync(2026, 10));
            var restored = await store.GetDashboardAsync(2026, 10);
            Assert.Equal(initial.MonthlyExpense, restored.MonthlyExpense);
            Assert.Equal(initial.Accounts.First(item => item.Id == account.Id).CurrentBalance,
                restored.Accounts.First(item => item.Id == account.Id).CurrentBalance);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task RecurringPayableGeneratesMonthlyPendingItemsAndPaymentCreatesExpenseOnce()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");

        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            var initial = await store.GetDashboardAsync(2026, 9);
            var account = initial.Accounts.First();
            var category = initial.Categories.First(item => item.Type == TransactionType.Expense);
            await store.AddRecurringPayableAsync(new CreateRecurringPayableRequest(
                "Aluguel", 1200m, 10, new DateOnly(2026, 9, 10), null, account.Id, category.Id));

            var septemberPayable = Assert.Single(await store.GetPayablesAsync(2026, 9));
            var octoberPayable = Assert.Single(await store.GetPayablesAsync(2026, 10));
            Assert.Equal(new DateOnly(2026, 9, 10), septemberPayable.DueDate);
            Assert.Equal(new DateOnly(2026, 10, 10), octoberPayable.DueDate);
            Assert.True(septemberPayable.IsRecurring);
            await store.DeletePayableAsync(octoberPayable.Id);
            Assert.Empty(await store.GetPayablesAsync(2026, 10));
            Assert.Empty(await store.GetPayablesAsync(2026, 10));
            Assert.Single(await store.GetPayablesAsync(2026, 11));

            var beforePayment = await store.GetDashboardAsync(2026, 9);
            Assert.Equal(0m, beforePayment.MonthlyExpense);
            await store.PayPayableAsync(new PayPayableRequest(
                septemberPayable.Id, new DateOnly(2026, 9, 10), Guid.NewGuid(), "Alex"));

            var afterPayment = await store.GetDashboardAsync(2026, 9);
            Assert.Equal(1200m, afterPayment.MonthlyExpense);
            Assert.Equal("Paga", Assert.Single(await store.GetPayablesAsync(2026, 9)).Status);
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.PayPayableAsync(new PayPayableRequest(
                septemberPayable.Id, new DateOnly(2026, 9, 10), Guid.NewGuid(), "Alex")));

            var recurringRule = Assert.Single(await store.GetRecurringPayablesAsync());
            await store.DeleteRecurringPayableAsync(recurringRule.Id);
            Assert.Empty(await store.GetRecurringPayablesAsync());
            Assert.Empty(await store.GetPayablesAsync(2026, 10));
            Assert.Empty(await store.GetPayablesAsync(2026, 11));
            Assert.Equal("Paga", Assert.Single(await store.GetPayablesAsync(2026, 9)).Status);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task DeletingPaidRecurringOccurrenceRemovesPaymentAndPreservesFollowingMonths()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            var initial = await store.GetDashboardAsync(2026, 10);
            var account = initial.Accounts[0];
            var category = initial.Categories.First(item => item.Type == TransactionType.Expense);
            await store.AddRecurringPayableAsync(new CreateRecurringPayableRequest(
                "Academia", 129.90m, 5, new DateOnly(2026, 10, 5), null, account.Id, category.Id));
            var october = Assert.Single(await store.GetPayablesAsync(2026, 10));
            await store.PayPayableAsync(new PayPayableRequest(
                october.Id, new DateOnly(2026, 10, 5), Guid.NewGuid(), "Alex"));
            await store.DeletePayableAsync(october.Id);

            Assert.Empty(await store.GetPayablesAsync(2026, 10));
            Assert.Empty(await store.GetPayablesAsync(2026, 10));
            Assert.Single(await store.GetPayablesAsync(2026, 11));
            var corrected = await store.GetDashboardAsync(2026, 10);
            Assert.Equal(initial.MonthlyExpense, corrected.MonthlyExpense);
            Assert.Equal(initial.AvailableBalance, corrected.AvailableBalance);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task IncomeAndExpenseCorrectlyCrossZeroInBothDirections()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            await store.AddAccountAsync(new CreateAccountRequest("Saldo positivo", FinancialAccountType.Checking, 100m));
            await store.AddAccountAsync(new CreateAccountRequest("Saldo negativo", FinancialAccountType.Checking, -100m));
            var dashboard = await store.GetDashboardAsync(2026, 9);
            var positive = dashboard.Accounts.Single(item => item.Name == "Saldo positivo");
            var negative = dashboard.Accounts.Single(item => item.Name == "Saldo negativo");
            var expense = dashboard.Categories.First(item => item.Type == TransactionType.Expense);
            var income = dashboard.Categories.First(item => item.Type == TransactionType.Income);

            await store.AddTransactionAsync(new CreateTransactionRequest(
                positive.Id, expense.Id, TransactionType.Expense, "Zerar positivo", 100m,
                new DateOnly(2026, 9, 15), Guid.NewGuid(), "Alex"));
            await store.AddTransactionAsync(new CreateTransactionRequest(
                negative.Id, income.Id, TransactionType.Income, "Zerar negativo", 100m,
                new DateOnly(2026, 9, 15), Guid.NewGuid(), "Alex"));

            var result = await store.GetDashboardAsync(2026, 9);
            Assert.Equal(0m, result.Accounts.Single(item => item.Id == positive.Id).CurrentBalance);
            Assert.Equal(0m, result.Accounts.Single(item => item.Id == negative.Id).CurrentBalance);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task ReserveMovementsChangeAvailableBalanceWithoutCreatingExpense()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            var initial = await store.GetDashboardAsync(2026, 9);
            await store.AddReserveAsync(new CreateReserveRequest("Emergencia"));
            var reserve = Assert.Single(await store.GetReservesAsync());

            await store.AddReserveMovementAsync(new AddReserveMovementRequest(
                reserve.Id, initial.Accounts[0].Id, 500m, true, new DateOnly(2026, 9, 15)));
            var afterDeposit = await store.GetDashboardAsync(2026, 9);
            Assert.Equal(initial.AvailableBalance - 500m, afterDeposit.AvailableBalance);
            Assert.Equal(500m, afterDeposit.TotalReserved);
            Assert.Equal(0m, afterDeposit.MonthlyExpense);

            await store.AddReserveMovementAsync(new AddReserveMovementRequest(
                reserve.Id, initial.Accounts[0].Id, 200m, false, new DateOnly(2026, 9, 16)));
            var afterWithdrawal = await store.GetDashboardAsync(2026, 9);
            Assert.Equal(initial.AvailableBalance - 300m, afterWithdrawal.AvailableBalance);
            Assert.Equal(300m, Assert.Single(await store.GetReservesAsync()).Balance);
            var history = await store.GetReserveMovementsAsync();
            Assert.Equal(2, history.Count);
            Assert.Contains(history, item => item.IsDeposit && item.Amount == 500m);
            Assert.Contains(history, item => !item.IsDeposit && item.Amount == 200m);
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.AddReserveMovementAsync(
                new AddReserveMovementRequest(reserve.Id, initial.Accounts[0].Id, 301m, false,
                    new DateOnly(2026, 9, 17))));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task DepositBeforeSalariesDoesNotRequireCompensatingExpense()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            var initial = await store.GetDashboardAsync(2026, 10);
            var account = initial.Accounts[0];
            var incomeCategory = initial.Categories.First(item => item.Type == TransactionType.Income);
            var expenseCategory = initial.Categories.First(item => item.Type == TransactionType.Expense);
            await store.AddReserveAsync(new CreateReserveRequest("Emergencia"));
            var reserve = Assert.Single(await store.GetReservesAsync());
            await store.AddReserveMovementAsync(new AddReserveMovementRequest(
                reserve.Id, account.Id, 500m, true, new DateOnly(2026, 10, 1)));
            await store.AddTransactionAsync(new CreateTransactionRequest(
                account.Id, incomeCategory.Id, TransactionType.Income, "Salario Alex", 2000m,
                new DateOnly(2026, 10, 2), Guid.NewGuid(), "Alex"));
            await store.AddTransactionAsync(new CreateTransactionRequest(
                account.Id, incomeCategory.Id, TransactionType.Income, "Salario Bia", 1800m,
                new DateOnly(2026, 10, 2), Guid.NewGuid(), "Bia"));
            var afterSalaries = await store.GetDashboardAsync(2026, 10);
            Assert.Equal(3800m, afterSalaries.MonthlyIncome);
            Assert.Equal(0m, afterSalaries.MonthlyExpense);
            Assert.Equal(initial.AvailableBalance + 3300m, afterSalaries.AvailableBalance);

            await store.AddTransactionAsync(new CreateTransactionRequest(
                account.Id, expenseCategory.Id, TransactionType.Expense, "Ajuste indevido", 500m,
                new DateOnly(2026, 10, 2), Guid.NewGuid(), "Alex"));
            var adjustment = Assert.Single(await store.GetTransactionsAsync(2026, 10),
                item => item.Description == "Ajuste indevido");
            await store.DeleteTransactionAsync(adjustment.Id);
            var corrected = await store.GetDashboardAsync(2026, 10);
            Assert.Equal(0m, corrected.MonthlyExpense);
            Assert.Equal(afterSalaries.AvailableBalance, corrected.AvailableBalance);
            Assert.Equal(500m, corrected.TotalReserved);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task RemovingMistakenDepositCanPreserveAvailableBalanceWithoutExpenseOrHistory()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            var before = await store.GetDashboardAsync(2026, 10);
            await store.AddReserveAsync(new CreateReserveRequest("Emergencia"));
            var reserve = Assert.Single(await store.GetReservesAsync());
            await store.AddReserveMovementAsync(new AddReserveMovementRequest(
                reserve.Id, before.Accounts[0].Id, 635.03m, true, new DateOnly(2026, 10, 4)));
            var withDeposit = await store.GetDashboardAsync(2026, 10);
            var movement = Assert.Single(await store.GetReserveMovementsAsync());
            await store.RemoveReserveDepositWithoutBalanceIncreaseAsync(movement.Id);

            var corrected = await store.GetDashboardAsync(2026, 10);
            Assert.Equal(0m, corrected.TotalReserved);
            Assert.Equal(withDeposit.AvailableBalance, corrected.AvailableBalance);
            Assert.Equal(withDeposit.MonthlyExpense, corrected.MonthlyExpense);
            Assert.Empty(await store.GetReserveMovementsAsync());
            Assert.Empty(await store.GetTransactionsAsync(2026, 10));
            Assert.Equal(before.AvailableBalance, (await store.GetDashboardAsync(2026, 9)).AvailableBalance);
            Assert.Equal(0m, Assert.Single(await store.GetReservesAsync()).Balance);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task GoalUsesLinkedReserveBalanceAsProgress()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            var dashboard = await store.GetDashboardAsync(2026, 9);
            await store.AddReserveAsync(new CreateReserveRequest("Apartamento"));
            var reserve = Assert.Single(await store.GetReservesAsync());
            await store.AddReserveMovementAsync(new AddReserveMovementRequest(
                reserve.Id, dashboard.Accounts[0].Id, 1200m, true, new DateOnly(2026, 9, 20)));
            await store.AddGoalAsync(new CreateGoalRequest(
                "Entrada", 50000m, new DateOnly(2028, 12, 31), 1500m, reserve.Id));

            var goal = Assert.Single(await store.GetGoalsAsync());
            Assert.Equal("Entrada", goal.Name);
            Assert.Equal(50000m, goal.TargetAmount);
            Assert.Equal(1200m, goal.CurrentAmount);
            Assert.Equal(1200m, goal.AverageMonthlyContribution);

            var reopened = new SqliteFinanceStore(databasePath);
            await reopened.InitializeAsync();
            Assert.Equal(1200m, Assert.Single(await reopened.GetGoalsAsync()).CurrentAmount);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task MonthlyTransactionsExportToExcelCompatibleCsv()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), $"nickfinancas-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var databasePath = Path.Combine(testDirectory, "nickfinancas.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            var dashboard = await store.GetDashboardAsync(2026, 9);
            var account = dashboard.Accounts.First();
            var category = dashboard.Categories.First(item => item.Type == TransactionType.Expense);
            await store.AddTransactionAsync(new CreateTransactionRequest(
                account.Id, category.Id, TransactionType.Expense, "Cafe; \"especial\"", 12.34m,
                new DateOnly(2026, 9, 15), Guid.NewGuid(), "Maria"));

            var result = await store.ExportTransactionsCsvAsync(2026, 9);

            Assert.Equal(1, result.TransactionCount);
            Assert.True(File.Exists(result.Path));
            var csv = await File.ReadAllTextAsync(result.Path);
            Assert.Contains("Data;Tipo;Descricao;Categoria;Origem;Destino;Cartao;Valor;Autor;Dispositivo", csv);
            Assert.Contains("\"Cafe; \"\"especial\"\"\"", csv);
            Assert.Contains("\"12,34\"", csv);
            Assert.Contains("\"Maria\"", csv);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(testDirectory)) Directory.Delete(testDirectory, true);
        }
    }

    [Fact]
    public async Task MembersPersistAndPrimaryAdministratorCannotBeRemoved()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            var space = Assert.Single(await store.GetFinancialSpacesAsync());
            var primary = Assert.Single(await store.GetMembersAsync(space.Id));
            Assert.True(primary.IsPrimary);
            Assert.Equal(FinancialSpaceRole.Administrator, primary.Role);

            await store.AddMemberAsync(new AddMemberRequest(space.Id, "Maria", FinancialSpaceRole.Member));
            var members = await store.GetMembersAsync(space.Id);
            var maria = members.Single(item => !item.IsPrimary);
            Assert.Equal("Maria", maria.Name);
            Assert.False((await store.GetLoginUsersAsync()).Single(item => item.Name == "Maria").HasPin);
            await store.SetMemberPinAsync("Maria", "2580");
            Assert.True(await store.AuthenticateMemberAsync("Maria", "2580"));
            Assert.False(await store.AuthenticateMemberAsync("Maria", "1111"));
            Assert.True((await store.GetLoginUsersAsync()).Single(item => item.Name == "Maria").HasPin);
            await store.UpdateMemberRoleAsync(new UpdateMemberRoleRequest(
                maria.Id, FinancialSpaceRole.Administrator));
            Assert.Equal(FinancialSpaceRole.Administrator,
                (await store.GetMembersAsync(space.Id)).Single(item => item.Id == maria.Id).Role);
            await store.UpdateMemberRoleAsync(new UpdateMemberRoleRequest(
                maria.Id, FinancialSpaceRole.Member));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.DeleteMemberAsync(primary.Id));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.UpdateMemberRoleAsync(
                new UpdateMemberRoleRequest(primary.Id, FinancialSpaceRole.Member)));

            var reopened = new SqliteFinanceStore(databasePath);
            await reopened.InitializeAsync();
            Assert.Equal(2, (await reopened.GetMembersAsync(space.Id)).Count);
            await reopened.DeleteMemberAsync(maria.Id);
            Assert.Single(await reopened.GetMembersAsync(space.Id));

            await reopened.AddFinancialSpaceAsync("Viagem", "Bia");
            var travel = (await reopened.GetFinancialSpacesAsync()).Single(item => item.Name == "Viagem");
            Assert.False(travel.IsPrimary);
            Assert.Equal("Bia", Assert.Single(await reopened.GetMembersAsync(travel.Id)).Name);
            await reopened.RenameFinancialSpaceAsync(travel.Id, "Viagem 2027");
            Assert.Contains(await reopened.GetFinancialSpacesAsync(), item => item.Name == "Viagem 2027");
            await reopened.AddMemberAsync(new AddMemberRequest(travel.Id, "Maria", FinancialSpaceRole.Member));
            Assert.Equal(2, (await reopened.GetMembersAsync(travel.Id)).Count);
            await reopened.DeleteFinancialSpaceAsync(travel.Id);
            Assert.DoesNotContain(await reopened.GetFinancialSpacesAsync(), item => item.Id == travel.Id);
            await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.DeleteFinancialSpaceAsync(space.Id));

            await reopened.AddFinancialSpaceAsync("Novo lar", "Alex");
            var newSpace = (await reopened.GetFinancialSpacesAsync()).Single(item => item.Name == "Novo lar");
            await reopened.DeleteFinancialSpaceAsync(space.Id, newSpace.Id);
            var remaining = Assert.Single(await reopened.GetFinancialSpacesAsync());
            Assert.True(remaining.IsPrimary);
            Assert.Equal("Novo lar", remaining.Name);
            Assert.Equal("Alex", Assert.Single(await reopened.GetMembersAsync(remaining.Id)).Name);
            await reopened.InitializeAsync();
            Assert.Single(await reopened.GetFinancialSpacesAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task InitializationRestoresMissingPrimaryAdministratorWhenOtherMembersExist()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath, seedSampleData: false);
            await store.InitializeAsync();
            var space = Assert.Single(await store.GetFinancialSpacesAsync());
            await store.AddMemberAsync(new AddMemberRequest(space.Id, "Maria", FinancialSpaceRole.Member));

            await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM members WHERE Name = 'Alex'";
                await command.ExecuteNonQueryAsync();
            }

            await store.InitializeAsync();
            var members = await store.GetMembersAsync(space.Id);
            Assert.Equal(2, members.Count);
            Assert.Equal(FinancialSpaceRole.Administrator,
                members.Single(item => item.Name == "Alex").Role);
            Assert.True(members.Single(item => item.Name == "Alex").IsPrimary);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task InitializationNormalizesLegacyLowercaseMemberSpaceId()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath, seedSampleData: false);
            await store.InitializeAsync();
            var space = Assert.Single(await store.GetFinancialSpacesAsync());

            await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "UPDATE members SET SpaceId = lower(SpaceId)";
                await command.ExecuteNonQueryAsync();
            }

            Assert.Empty(await store.GetMembersAsync(space.Id));
            await store.InitializeAsync();
            var owner = Assert.Single(await store.GetMembersAsync(space.Id));
            Assert.Equal("Alex", owner.Name);
            Assert.Equal(FinancialSpaceRole.Administrator, owner.Role);
            Assert.True(owner.IsPrimary);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task BackupRestoresDatabaseToCapturedState()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), $"nickfinancas-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var databasePath = Path.Combine(testDirectory, "nickfinancas.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            await store.AddReserveAsync(new CreateReserveRequest("Preservada"));
            var backup = await store.CreateBackupAsync();
            Assert.True(File.Exists(backup.Path));
            await using (var validation = new SqliteConnection($"Data Source={backup.Path};Mode=ReadOnly;Pooling=False"))
            {
                await validation.OpenAsync();
                await using var check = validation.CreateCommand();
                check.CommandText = "PRAGMA integrity_check";
                Assert.Equal("ok", await check.ExecuteScalarAsync());
            }
            Assert.Single(await store.GetBackupsAsync());
            var maintenance = await store.EnsureDailyBackupAsync();
            Assert.False(maintenance.Created);
            Assert.Equal(1, maintenance.AvailableBackups);

            await store.AddReserveAsync(new CreateReserveRequest("Depois do backup"));
            Assert.Equal(2, (await store.GetReservesAsync()).Count);
            await store.RestoreBackupAsync(backup.Path);

            var reserves = await store.GetReservesAsync();
            Assert.Single(reserves);
            Assert.Equal("Preservada", reserves[0].Name);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(testDirectory)) Directory.Delete(testDirectory, true);
        }
    }

    [Fact]
    public async Task DailyBackupIsCreatedOnceAndRetentionKeepsTenCopies()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), $"nickfinancas-retention-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var databasePath = Path.Combine(testDirectory, "nickfinancas.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            var daily = await store.EnsureDailyBackupAsync();
            Assert.True(daily.Created);
            for (var index = 0; index < 11; index++)
            {
                await Task.Delay(2);
                await store.CreateBackupAsync();
            }
            Assert.Equal(10, (await store.GetBackupsAsync()).Count);
            Assert.False((await store.EnsureDailyBackupAsync()).Created);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(testDirectory)) Directory.Delete(testDirectory, true);
        }
    }

    [Fact]
    public async Task CreditCardPurchaseIsSplitAcrossFutureInvoicesWithoutRoundingLoss()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            var dashboard = await store.GetDashboardAsync(2026, 9);
            var account = dashboard.Accounts[0];
            var category = dashboard.Categories.First(item => item.Type == TransactionType.Expense);
            await store.AddCreditCardAsync(new CreateCreditCardRequest("Parcelado", account.Id, 5000m, 20, 28));
            var card = Assert.Single(await store.GetCreditCardsAsync());

            await store.AddTransactionAsync(new CreateTransactionRequest(
                account.Id, category.Id, TransactionType.Expense, "Notebook", 100m,
                new DateOnly(2026, 9, 10), Guid.NewGuid(), "Alex", null, card.Id, 3));

            var september = Assert.Single(await store.GetTransactionsAsync(2026, 9));
            var october = Assert.Single(await store.GetTransactionsAsync(2026, 10));
            var november = Assert.Single(await store.GetTransactionsAsync(2026, 11));
            Assert.Equal("Notebook (1/3)", september.Description);
            Assert.Equal(33.33m, september.Amount);
            Assert.Equal(33.33m, october.Amount);
            Assert.Equal(33.34m, november.Amount);
            Assert.Equal(100m, september.Amount + october.Amount + november.Amount);
            Assert.Equal(33.33m, (await store.GetCreditCardInvoicesAsync(2026, 9)).Single().Total);
            Assert.Equal(33.33m, (await store.GetCreditCardInvoicesAsync(2026, 10)).Single().Total);
            Assert.Equal(33.34m, (await store.GetCreditCardInvoicesAsync(2026, 11)).Single().Total);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task CreditCardPurchaseCanRegisterOnlyRemainingInstallmentsAndTrackInvoicePayments()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            var dashboard = await store.GetDashboardAsync(2026, 1);
            var account = dashboard.Accounts[0];
            var category = dashboard.Categories.First(item => item.Type == TransactionType.Expense);
            await store.AddCreditCardAsync(new CreateCreditCardRequest("Cartao", account.Id, 5000m, 20, 28));
            var card = Assert.Single(await store.GetCreditCardsAsync());

            await store.AddTransactionAsync(new CreateTransactionRequest(
                account.Id, category.Id, TransactionType.Expense, "Patinete", 1000m,
                new DateOnly(2026, 1, 10), Guid.NewGuid(), "Alex", CreditCardId: card.Id,
                InstallmentCount: 10, InstallmentStart: 9));

            Assert.Empty(await store.GetTransactionsAsync(2026, 1));
            Assert.Equal("Patinete (9/10)", Assert.Single(await store.GetTransactionsAsync(2026, 9)).Description);
            Assert.Equal("Patinete (10/10)", Assert.Single(await store.GetTransactionsAsync(2026, 10)).Description);
            var plan = Assert.Single(await store.GetInstallmentPlansAsync());
            Assert.Equal(10, plan.TotalCount);
            Assert.Equal(2, plan.RemainingCount);
            Assert.Equal(100m, plan.NextAmount);
            Assert.Equal(new DateOnly(2026, 9, 28), plan.NextDueDate);

            await store.PayCreditCardInvoiceAsync(new PayCreditCardInvoiceRequest(
                card.Id, 2026, 9, new DateOnly(2026, 9, 28), Guid.NewGuid(), "Alex"));
            plan = Assert.Single(await store.GetInstallmentPlansAsync());
            Assert.Equal(1, plan.RemainingCount);
            Assert.Equal(new DateOnly(2026, 10, 28), plan.NextDueDate);

            await store.PayCreditCardInvoiceAsync(new PayCreditCardInvoiceRequest(
                card.Id, 2026, 10, new DateOnly(2026, 10, 28), Guid.NewGuid(), "Alex"));
            Assert.Empty(await store.GetInstallmentPlansAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task DeletingRemainingAccountInstallmentsPreservesPaidOnes()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            var dashboard = await store.GetDashboardAsync(2026, 10);
            var account = dashboard.Accounts.First();
            var category = dashboard.Categories.First(item => item.Type == TransactionType.Expense);
            await store.AddPayableAsync(new CreatePayableRequest(
                "Patinete", 137m, new DateOnly(2026, 10, 30), account.Id, category.Id, 3));
            var paid = Assert.Single(await store.GetPayablesAsync(2026, 10));
            await store.PayPayableAsync(new PayPayableRequest(
                paid.Id, new DateOnly(2026, 10, 30), Guid.NewGuid(), "Alex"));
            var plan = Assert.Single(await store.GetInstallmentPlansAsync());
            Assert.True(plan.IsAccountPayable);
            Assert.Equal(2, plan.RemainingCount);

            await store.DeleteRemainingInstallmentsAsync(plan.GroupId);

            Assert.Empty(await store.GetInstallmentPlansAsync());
            Assert.Empty(await store.GetPayablesAsync(2026, 11));
            Assert.Empty(await store.GetPayablesAsync(2026, 12));
            Assert.Equal("Paga", Assert.Single(await store.GetPayablesAsync(2026, 10)).Status);
            Assert.Equal(137m, (await store.GetDashboardAsync(2026, 10)).MonthlyExpense);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task PayableInstallmentsTrackOnlyRemainingPayments()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            var dashboard = await store.GetDashboardAsync(2026, 9);
            var account = dashboard.Accounts[0];
            var category = dashboard.Categories.First(item => item.Type == TransactionType.Expense);

            await store.AddPayableAsync(new CreatePayableRequest(
                "Patinete", 100m, new DateOnly(2026, 9, 15), account.Id, category.Id, 10, 9));

            Assert.Empty(await store.GetPayablesAsync(2026, 8));
            var september = Assert.Single(await store.GetPayablesAsync(2026, 9));
            var october = Assert.Single(await store.GetPayablesAsync(2026, 10));
            Assert.Equal("Patinete (9/10)", september.Description);
            Assert.Equal("Patinete (10/10)", october.Description);
            Assert.Equal(2, Assert.Single(await store.GetInstallmentPlansAsync()).RemainingCount);

            await store.PayPayableAsync(new PayPayableRequest(
                september.Id, new DateOnly(2026, 9, 15), Guid.NewGuid(), "Alex"));
            Assert.Equal(1, Assert.Single(await store.GetInstallmentPlansAsync()).RemainingCount);
            Assert.Equal("Patinete (9/10)", Assert.Single(await store.GetTransactionsAsync(2026, 9)).Description);

            await store.PayPayableAsync(new PayPayableRequest(
                october.Id, new DateOnly(2026, 10, 15), Guid.NewGuid(), "Alex"));
            Assert.Empty(await store.GetInstallmentPlansAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task DirectPurchasePaysFirstInstallmentAndSchedulesTheRest()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            var dashboard = await store.GetDashboardAsync(2026, 9);
            var account = dashboard.Accounts[0];
            var category = dashboard.Categories.First(item => item.Type == TransactionType.Expense);

            await store.AddTransactionAsync(new CreateTransactionRequest(
                account.Id, category.Id, TransactionType.Expense, "Compra", 100m,
                new DateOnly(2026, 9, 10), Guid.NewGuid(), "Alex", InstallmentCount: 3));

            var paid = Assert.Single(await store.GetTransactionsAsync(2026, 9));
            Assert.Equal("Compra (1/3)", paid.Description);
            Assert.Equal(33.33m, paid.Amount);
            Assert.Empty(await store.GetPayablesAsync(2026, 9));
            Assert.Equal(33.33m, Assert.Single(await store.GetPayablesAsync(2026, 10)).Amount);
            Assert.Equal(33.34m, Assert.Single(await store.GetPayablesAsync(2026, 11)).Amount);
            Assert.Equal(2, Assert.Single(await store.GetInstallmentPlansAsync()).RemainingCount);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task SubcategoryKeepsHierarchyAndMustMatchParentType()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            await store.AddCategoryAsync(new CreateCategoryRequest("Transporte", TransactionType.Expense));
            var parent = (await store.GetDashboardAsync(2026, 9)).Categories.Single(item => item.Name == "Transporte");
            await store.AddCategoryAsync(new CreateCategoryRequest("Combustivel", TransactionType.Expense, parent.Id));

            var child = (await store.GetDashboardAsync(2026, 9)).Categories.Single(item => item.Name == "Combustivel");
            Assert.Equal(parent.Id, child.ParentCategoryId);
            Assert.Equal("Transporte", child.ParentName);
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.AddCategoryAsync(
                new CreateCategoryRequest("Invalida", TransactionType.Income, parent.Id)));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.DeleteCategoryAsync(parent.Id));
            await store.DeleteCategoryAsync(child.Id);
            await store.DeleteCategoryAsync(parent.Id);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task ReservesAndGoalsCanBeEditedAndAreSafelyDeleted()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            await store.AddReserveAsync(new CreateReserveRequest("Nome antigo"));
            var reserve = Assert.Single(await store.GetReservesAsync());
            await store.UpdateReserveAsync(new UpdateReserveRequest(reserve.Id, "Emergencia"));
            Assert.Equal("Emergencia", Assert.Single(await store.GetReservesAsync()).Name);

            await store.AddGoalAsync(new CreateGoalRequest("Meta antiga", 1000m, null, 100m, reserve.Id));
            var goal = Assert.Single(await store.GetGoalsAsync());
            await store.UpdateGoalAsync(new UpdateGoalRequest(
                goal.Id, "Meta atualizada", 2000m, new DateOnly(2027, 12, 31), 200m, reserve.Id));
            var updated = Assert.Single(await store.GetGoalsAsync());
            Assert.Equal("Meta atualizada", updated.Name);
            Assert.Equal(2000m, updated.TargetAmount);
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.DeleteReserveAsync(reserve.Id));

            await store.DeleteGoalAsync(goal.Id);
            Assert.Empty(await store.GetGoalsAsync());
            await store.DeleteReserveAsync(reserve.Id);
            Assert.Empty(await store.GetReservesAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task AGoalCanBeSelectedAsTheOnlyPrimaryGoal()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            await store.AddGoalAsync(new CreateGoalRequest("Primeiro", 1000m, null, 100m, null));
            await store.AddGoalAsync(new CreateGoalRequest("Segundo", 2000m, null, 200m, null));

            var initial = await store.GetGoalsAsync();
            Assert.True(initial.Single(item => item.Name == "Primeiro").IsPrimary);
            Assert.False(initial.Single(item => item.Name == "Segundo").IsPrimary);

            await store.SetPrimaryGoalAsync(initial.Single(item => item.Name == "Segundo").Id);
            var updated = await store.GetGoalsAsync();
            Assert.False(updated.Single(item => item.Name == "Primeiro").IsPrimary);
            Assert.True(updated.Single(item => item.Name == "Segundo").IsPrimary);
            Assert.Single(updated, item => item.IsPrimary);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task TransferBetweenReservesPreservesTotalAndAvailableBalance()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            var initial = await store.GetDashboardAsync(2026, 9);
            await store.AddReserveAsync(new CreateReserveRequest("Origem"));
            await store.AddReserveAsync(new CreateReserveRequest("Destino"));
            var reserves = await store.GetReservesAsync();
            var source = reserves.Single(item => item.Name == "Origem");
            var destination = reserves.Single(item => item.Name == "Destino");
            await store.AddReserveMovementAsync(new AddReserveMovementRequest(
                source.Id, initial.Accounts[0].Id, 500m, true, new DateOnly(2026, 9, 10)));
            var before = await store.GetDashboardAsync(2026, 9);

            await store.TransferReserveAsync(new TransferReserveRequest(
                source.Id, destination.Id, initial.Accounts[0].Id, 200m, new DateOnly(2026, 9, 11)));

            var after = await store.GetDashboardAsync(2026, 9);
            var result = await store.GetReservesAsync();
            Assert.Equal(300m, result.Single(item => item.Id == source.Id).Balance);
            Assert.Equal(200m, result.Single(item => item.Id == destination.Id).Balance);
            Assert.Equal(before.TotalReserved, after.TotalReserved);
            Assert.Equal(before.AvailableBalance, after.AvailableBalance);
            Assert.Equal(0m, after.MonthlyExpense);
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.TransferReserveAsync(
                new TransferReserveRequest(source.Id, destination.Id, initial.Accounts[0].Id, 301m,
                    new DateOnly(2026, 9, 12))));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task LocalChangesArePersistedInOfflineSyncQueue()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteFinanceStore(databasePath);
            await store.InitializeAsync();
            var before = await CountOutboxAsync(databasePath);

            await store.AddAccountAsync(new CreateAccountRequest(
                "Conta para sincronizar", FinancialAccountType.Checking, 125m));

            var after = await CountOutboxAsync(databasePath);
            Assert.Equal(before + 1, after);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    private static async Task<long> CountOutboxAsync(string databasePath)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sync_outbox;";
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }
}
