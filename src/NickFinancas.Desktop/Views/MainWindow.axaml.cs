using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NickFinancas.Desktop.ViewModels;

namespace NickFinancas.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void SelectBankCsv_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecionar extrato CSV do Nubank",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Arquivos CSV") { Patterns = ["*.csv"] }],
        });
        if (files.Count == 0 || DataContext is not MainViewModel viewModel) return;
        var path = files[0].TryGetLocalPath();
        if (path is not null) await viewModel.LoadNubankCsvAsync(path);
    }
}
