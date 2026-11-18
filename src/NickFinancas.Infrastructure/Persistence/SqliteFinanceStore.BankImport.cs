using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using NickFinancas.Application.Finance;
using NickFinancas.Domain.Transactions;

namespace NickFinancas.Infrastructure.Persistence;

public sealed partial class SqliteFinanceStore
{
    public async Task<IReadOnlyList<BankImportPreviewItem>> PreviewNubankCsvAsync(
        string path, Guid accountId, CancellationToken cancellationToken = default)
    {
        if (new FileInfo(path).Length > 10_000_000)
            throw new InvalidOperationException("O CSV excede o limite de 10 MB.");

        await using var database = new LocalFinanceDbContext(_options);
        if (!await database.Accounts.AnyAsync(item => item.Id == accountId, cancellationToken))
            throw new InvalidOperationException("Selecione uma conta existente.");

        var parsed = new List<(Guid Id, string BankIdentifier, DateOnly Date, decimal SignedAmount, string Description)>();
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        using var parser = new TextFieldParser(path, Encoding.UTF8)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
        };
        parser.SetDelimiters(",");
        var header = parser.ReadFields();
        if (header is null || header.Length != 4 ||
            !header[0].TrimStart('\uFEFF').Equals("Data", StringComparison.OrdinalIgnoreCase) ||
            !header[1].Equals("Valor", StringComparison.OrdinalIgnoreCase) ||
            !header[2].Equals("Identificador", StringComparison.OrdinalIgnoreCase) ||
            !header[3].Equals("Descrição", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Este arquivo não tem as quatro colunas esperadas do extrato Nubank.");

        while (!parser.EndOfData)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string[]? fields;
            try { fields = parser.ReadFields(); }
            catch (MalformedLineException)
            {
                throw new InvalidOperationException($"A linha {parser.ErrorLineNumber} do CSV está malformada.");
            }
            if (fields is null) continue;
            if (fields.Length != 4 ||
                !DateOnly.TryParseExact(fields[0], "dd/MM/yyyy", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date) ||
                !decimal.TryParse(fields[1], NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out var signedAmount) || signedAmount == 0 ||
                signedAmount > MaxMonetaryAmount || signedAmount < -MaxMonetaryAmount ||
                string.IsNullOrWhiteSpace(fields[2]) || string.IsNullOrWhiteSpace(fields[3]))
                throw new InvalidOperationException($"A linha {parser.LineNumber} contém dados inválidos.");

            var description = fields[3].Trim();
            if (description.Length > 180) description = description[..180];
            var identity = string.Join("|", accountId.ToString("N"), fields[2].Trim(),
                date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                signedAmount.ToString("0.00", CultureInfo.InvariantCulture), description);
            var occurrence = occurrences.TryGetValue(identity, out var count) ? count + 1 : 1;
            occurrences[identity] = occurrence;
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"Nubank|{identity}|{occurrence}"));
            parsed.Add((new Guid(hash[..16]), fields[2].Trim(), date, signedAmount, description));
            if (parsed.Count > 50_000)
                throw new InvalidOperationException("O CSV excede o limite de 50 mil lançamentos.");
        }

        if (parsed.Count == 0) throw new InvalidOperationException("O CSV não contém lançamentos.");
        var start = parsed.Min(item => item.Date);
        var end = parsed.Max(item => item.Date);
        var existing = await database.Transactions.AsNoTracking()
            .Where(item => item.AccountId == accountId && item.Date >= start && item.Date <= end)
            .Select(item => new { item.Id, item.Date, item.Amount, item.Type, item.Description, item.IsDeleted })
            .ToListAsync(cancellationToken);
        var importedIds = existing.Select(item => item.Id).ToHashSet();
        var cardAdvanceIds = parsed.Where(item => item.Description.StartsWith(
                "Valor adicionado na conta por cartão de crédito", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.BankIdentifier).ToHashSet(StringComparer.Ordinal);

        return parsed.Select(item =>
        {
            var alreadyImported = importedIds.Contains(item.Id);
            var type = item.SignedAmount > 0 ? TransactionType.Income : TransactionType.Expense;
            var possibleMatch = !alreadyImported && existing.Any(saved => !saved.IsDeleted &&
                saved.Date == item.Date && saved.Type == type && saved.Amount == Math.Abs(item.SignedAmount));
            var special = item.Description.StartsWith("Pagamento de fatura", StringComparison.OrdinalIgnoreCase) ||
                cardAdvanceIds.Contains(item.BankIdentifier);
            var note = alreadyImported ? "Já importado" : possibleMatch ? "Possível lançamento existente" :
                special ? "Movimentação de cartão: revisar separadamente" : "Conferir categoria e natureza do Pix";
            return new BankImportPreviewItem(item.Id, item.Date, item.SignedAmount,
                item.Description, alreadyImported, possibleMatch, special, note);
        }).ToList();
    }

    public async Task<int> ImportBankTransactionsAsync(
        IReadOnlyList<BankImportTransactionRequest> transactions,
        CancellationToken cancellationToken = default)
    {
        if (transactions.Count == 0) return 0;
        if (transactions.Select(item => item.ImportId).Distinct().Count() != transactions.Count)
            throw new InvalidOperationException("A seleção contém lançamentos repetidos.");

        await using var database = new LocalFinanceDbContext(_options);
        await using var dbTransaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var accounts = await database.Accounts.AsNoTracking().Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var categories = await database.Categories.ToListAsync(cancellationToken);
        var requestedIds = transactions.Select(item => item.ImportId).ToArray();
        var existingIds = (await database.Transactions.AsNoTracking()
            .Where(item => requestedIds.Contains(item.Id))
            .Select(item => item.Id).ToListAsync(cancellationToken)).ToHashSet();
        var imported = 0;

        foreach (var item in transactions)
        {
            if (existingIds.Contains(item.ImportId)) continue;
            if (!accounts.Contains(item.AccountId) || item.Type is not (TransactionType.Income or TransactionType.Expense) ||
                item.Amount <= 0 || item.Amount > MaxMonetaryAmount || string.IsNullOrWhiteSpace(item.Description) ||
                item.Description.Length > 180 || string.IsNullOrWhiteSpace(item.AuthorName))
                throw new InvalidOperationException("Um lançamento selecionado é inválido.");

            var category = categories.FirstOrDefault(candidate => candidate.Id == item.CategoryId);
            if (item.CategoryId == Guid.Empty)
            {
                category = categories.FirstOrDefault(candidate => candidate.Type == item.Type &&
                    candidate.Name.Equals("Outro", StringComparison.OrdinalIgnoreCase) && candidate.ParentCategoryId is null);
                if (category is null)
                {
                    category = new CategoryRecord { Id = Guid.NewGuid(), Name = "Outro", Type = item.Type };
                    categories.Add(category);
                    database.Categories.Add(category);
                }
            }
            if (category is null || category.Type != item.Type)
                throw new InvalidOperationException("A categoria escolhida não corresponde ao tipo do lançamento.");

            database.Transactions.Add(new TransactionRecord
            {
                Id = item.ImportId, AccountId = item.AccountId, CategoryId = category.Id,
                Type = item.Type, Description = item.Description, Amount = item.Amount,
                Date = item.Date, AuthorId = item.AuthorId, AuthorName = item.AuthorName,
                DeviceName = Environment.MachineName, CreatedAtUtc = DateTime.UtcNow.AddTicks(imported), Version = 1,
            });
            imported++;
        }

        await database.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);
        return imported;
    }
}
