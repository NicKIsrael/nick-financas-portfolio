using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NickFinancas.Application.Finance;
using NickFinancas.Domain.Transactions;

namespace NickFinancas.Desktop.ViewModels;

public partial class MainViewModel
{
    [ObservableProperty] public partial bool IsBankImportOpen { get; set; }
    [ObservableProperty] public partial bool IsBankImportBusy { get; set; }
    [ObservableProperty] public partial AccountOption? SelectedImportAccount { get; set; }
    [ObservableProperty] public partial string ImportFileName { get; set; } = string.Empty;
    [ObservableProperty] public partial string? BankImportError { get; set; }
    [ObservableProperty] public partial string? BankImportNotice { get; set; }
    public ObservableCollection<BankImportRowViewModel> BankImportRows { get; } = [];
    private string? _bankImportPath;
    private Guid? _bankImportPreviewAccountId;

    public async Task LoadNubankCsvAsync(string path)
    {
        IsBankImportOpen = true;
        _bankImportPath = path;
        ImportFileName = Path.GetFileName(path);
        BankImportError = null;
        BankImportNotice = null;
        SelectedImportAccount ??= Accounts.FirstOrDefault();
        await RefreshBankImportPreviewAsync();
    }

    [RelayCommand]
    private async Task RefreshBankImportPreviewAsync()
    {
        if (_bankImportPath is null || SelectedImportAccount is null)
        {
            BankImportError = "Selecione um arquivo CSV e a conta correspondente.";
            return;
        }
        try
        {
            IsBankImportBusy = true;
            BankImportError = null;
            var preview = await _financeStore.PreviewNubankCsvAsync(_bankImportPath, SelectedImportAccount.Id);
            _bankImportPreviewAccountId = SelectedImportAccount.Id;
            BankImportRows.Clear();
            foreach (var item in preview)
            {
                var type = item.SignedAmount > 0 ? TransactionType.Income : TransactionType.Expense;
                var categories = _allCategories.Where(category => category.Type == type)
                    .Select(category => new BankImportCategoryOption(category.Id, category.Name))
                    .ToList();
                if (!categories.Any(category => category.Name.Equals("Outro", StringComparison.OrdinalIgnoreCase)))
                    categories.Insert(0, new BankImportCategoryOption(Guid.Empty, "Outro"));
                var suggested = categories.FirstOrDefault(category => category.Id != Guid.Empty &&
                    !category.Name.Equals("Outro", StringComparison.OrdinalIgnoreCase) &&
                    item.Description.Contains(category.Name, StringComparison.OrdinalIgnoreCase));
                BankImportRows.Add(new BankImportRowViewModel(item, categories,
                    suggested ?? categories.First(category => category.Name.Equals("Outro", StringComparison.OrdinalIgnoreCase))));
            }
            BankImportNotice = $"{BankImportRows.Count} linhas encontradas. Confira as categorias antes de confirmar.";
        }
        catch (Exception exception)
        {
            BankImportRows.Clear();
            _bankImportPreviewAccountId = null;
            BankImportError = $"Não foi possível ler o CSV: {exception.Message}";
        }
        finally { IsBankImportBusy = false; }
    }

    [RelayCommand]
    private async Task ConfirmBankImportAsync()
    {
        if (SelectedImportAccount is null) return;
        if (_bankImportPreviewAccountId != SelectedImportAccount.Id)
        {
            BankImportError = "A conta foi alterada. Atualize a prévia antes de importar.";
            return;
        }
        var selected = BankImportRows.Where(row => row.IsIncluded && row.CanImport).ToList();
        if (selected.Count == 0)
        {
            BankImportError = "Marque ao menos um lançamento para importar.";
            return;
        }
        try
        {
            IsBankImportBusy = true;
            BankImportError = null;
            var requests = selected.Select(row => new BankImportTransactionRequest(
                row.ImportId, SelectedImportAccount.Id, row.SelectedCategory.Id, row.Type,
                row.Description, Math.Abs(row.SignedAmount), row.Date, ActiveUserId, ActiveUserName)).ToList();
            var count = await _financeStore.ImportBankTransactionsAsync(requests);
            IsBankImportOpen = false;
            BankImportRows.Clear();
            LedgerSaveNotice = $"{count} lançamentos importados do Nubank.";
            await LoadAsync();
        }
        catch (Exception exception)
        {
            BankImportError = $"Não foi possível importar: {exception.Message}";
        }
        finally { IsBankImportBusy = false; }
    }

    [RelayCommand]
    private void CloseBankImport()
    {
        IsBankImportOpen = false;
        BankImportRows.Clear();
        BankImportError = null;
    }
}

public sealed record BankImportCategoryOption(Guid Id, string Name)
{
    public override string ToString() => Name;
}

public partial class BankImportRowViewModel : ObservableObject
{
    public BankImportRowViewModel(BankImportPreviewItem item,
        IReadOnlyList<BankImportCategoryOption> categories, BankImportCategoryOption selectedCategory)
    {
        ImportId = item.ImportId;
        Date = item.Date;
        SignedAmount = item.SignedAmount;
        Description = item.Description;
        Note = item.Note;
        CanImport = !item.AlreadyImported && !item.RequiresReview;
        IsIncluded = CanImport && !item.PossibleMatch;
        Categories = categories;
        SelectedCategory = selectedCategory;
    }

    public Guid ImportId { get; }
    public DateOnly Date { get; }
    public decimal SignedAmount { get; }
    public TransactionType Type => SignedAmount > 0 ? TransactionType.Income : TransactionType.Expense;
    public string DateText => Date.ToString("dd/MM/yyyy");
    public string AmountText => SignedAmount.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
    public string Description { get; }
    public string Note { get; }
    public bool CanImport { get; }
    public IReadOnlyList<BankImportCategoryOption> Categories { get; }
    [ObservableProperty] public partial bool IsIncluded { get; set; }
    [ObservableProperty] public partial BankImportCategoryOption SelectedCategory { get; set; }
}
