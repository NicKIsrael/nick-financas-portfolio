using Microsoft.Data.Sqlite;
using NickFinancas.Application.Finance;
using NickFinancas.Domain.Accounts;
using NickFinancas.Domain.Transactions;
using NickFinancas.Infrastructure.Persistence;

namespace NickFinancas.Infrastructure.Tests.Persistence;

public sealed class NubankCsvImportTests
{
    [Fact]
    public async Task RepeatedBankIdentifierDoesNotDropDistinctRowsAndReimportIsIdempotent()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        var csvPath = Path.Combine(Path.GetTempPath(), $"nubank-{Guid.NewGuid():N}.csv");
        try
        {
            await File.WriteAllTextAsync(csvPath,
                "Data,Valor,Identificador,Descrição\n" +
                "09/08/2026,25.00,shared-id,Transferência recebida pelo Pix\n" +
                "09/08/2026,-25.00,shared-id,Transferência enviada pelo Pix\n" +
                "10/08/2026,-12.50,other-id,Padaria\n");
            var store = new SqliteFinanceStore(databasePath, seedSampleData: false);
            await store.InitializeAsync();
            await store.AddAccountAsync(new CreateAccountRequest("Nubank", FinancialAccountType.Checking, 0));
            var account = Assert.Single((await store.GetDashboardAsync(2026, 8)).Accounts);

            var preview = await store.PreviewNubankCsvAsync(csvPath, account.Id);
            Assert.Equal(3, preview.Count);
            Assert.Equal(3, preview.Select(item => item.ImportId).Distinct().Count());
            Assert.All(preview, item => Assert.False(item.AlreadyImported));

            var requests = preview.Select(item => new BankImportTransactionRequest(
                item.ImportId, account.Id, Guid.Empty,
                item.SignedAmount > 0 ? TransactionType.Income : TransactionType.Expense,
                item.Description, Math.Abs(item.SignedAmount), item.Date, Guid.NewGuid(), "Teste")).ToList();
            Assert.Equal(3, await store.ImportBankTransactionsAsync(requests));
            Assert.Equal(0, await store.ImportBankTransactionsAsync(requests));
            Assert.All(await store.PreviewNubankCsvAsync(csvPath, account.Id),
                item => Assert.True(item.AlreadyImported));
            Assert.Equal(3, (await store.GetTransactionsAsync(2026, 8)).Count);
            var categories = (await store.GetDashboardAsync(2026, 8)).Categories;
            Assert.Contains(categories, item => item.Name == "Outro" && item.Type == TransactionType.Income);
            Assert.Contains(categories, item => item.Name == "Outro" && item.Type == TransactionType.Expense);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public async Task CardMovementsAreFlaggedForSeparateReview()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nickfinancas-{Guid.NewGuid():N}.db");
        var csvPath = Path.Combine(Path.GetTempPath(), $"nubank-{Guid.NewGuid():N}.csv");
        try
        {
            await File.WriteAllTextAsync(csvPath,
                "Data,Valor,Identificador,Descrição\n" +
                "19/09/2026,-119.68,invoice-id,Pagamento de fatura\n" +
                "19/09/2026,119.68,credit-id,Valor adicionado na conta por cartão de crédito - Pix\n" +
                "19/09/2026,-119.68,credit-id,Transferência enviada pelo Pix\n");
            var store = new SqliteFinanceStore(databasePath, seedSampleData: false);
            await store.InitializeAsync();
            await store.AddAccountAsync(new CreateAccountRequest("Nubank", FinancialAccountType.Checking, 0));
            var account = Assert.Single((await store.GetDashboardAsync(2026, 9)).Accounts);
            var preview = await store.PreviewNubankCsvAsync(csvPath, account.Id);
            Assert.All(preview, item => Assert.True(item.RequiresReview));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }
}
