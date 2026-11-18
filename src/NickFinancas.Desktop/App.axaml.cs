using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System.Diagnostics;
using System.Reflection;
using NickFinancas.Desktop.ViewModels;
using NickFinancas.Desktop.Views;
using NickFinancas.Infrastructure.Persistence;
using NickFinancas.Infrastructure.Sync;
using NickFinancas.Infrastructure.Updates;

namespace NickFinancas.Desktop;

public partial class App : Avalonia.Application
{
    private FinanceSyncService? _syncService;
    private PendingAppUpdate? _pendingUpdate;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var financeStore = SqliteFinanceStore.CreateDefault();
            financeStore.InitializeAsync().GetAwaiter().GetResult();
            financeStore.EnsureDailyBackupAsync().GetAwaiter().GetResult();
            var viewModel = new MainViewModel(financeStore);
            viewModel.LoadAsync().GetAwaiter().GetResult();

            var syncOptions = FinanceSyncOptions.LoadDefault();
            if (syncOptions is not null)
            {
                _syncService = new FinanceSyncService(financeStore, syncOptions);
                _syncService.RemoteDataApplied += (_, _) => Dispatcher.UIThread.Post(async () =>
                    await viewModel.LoadAsync());
                _syncService.Start();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var version = typeof(App).Assembly
                            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                            .Split('+')[0] ?? "0.0.0";
                        _pendingUpdate = await new AppUpdateService(syncOptions.ServerUrl, syncOptions.ApiKey)
                            .CheckAndDownloadAsync(version);
                    }
                    catch
                    {
                        // Atualizacoes sao tentadas novamente na proxima inicializacao sem impedir o uso offline.
                    }
                });
                desktop.Exit += (_, _) =>
                {
                    // Start the updater synchronously while the desktop process is still alive.
                    // An async Exit handler could be terminated at its first await, leaving an
                    // already downloaded update unapplied on the other computer.
                    if (_pendingUpdate is not null) LaunchUpdater(_pendingUpdate);
                    if (_syncService is not null)
                        _ = _syncService.DisposeAsync();
                };
            }

            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void LaunchUpdater(PendingAppUpdate update)
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable) ||
            !string.Equals(Path.GetExtension(executable), ".exe", StringComparison.OrdinalIgnoreCase))
            return;
        var updateDirectory = Path.GetDirectoryName(update.PackagePath)!;
        var updater = Path.Combine(updateDirectory, "NickFinancas.Updater.exe");
        File.Copy(executable, updater, true);
        var start = new ProcessStartInfo(updater) { UseShellExecute = true };
        start.ArgumentList.Add("--apply-update");
        start.ArgumentList.Add("--package");
        start.ArgumentList.Add(update.PackagePath);
        start.ArgumentList.Add("--target");
        start.ArgumentList.Add(AppContext.BaseDirectory);
        start.ArgumentList.Add("--pid");
        start.ArgumentList.Add(Environment.ProcessId.ToString());
        start.ArgumentList.Add("--executable");
        start.ArgumentList.Add(Path.GetFileName(executable));
        Process.Start(start);
    }
}
