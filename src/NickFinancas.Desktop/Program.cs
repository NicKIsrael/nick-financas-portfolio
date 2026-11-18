using System.Globalization;
using Avalonia;
using NickFinancas.Desktop.Services;

namespace NickFinancas.Desktop;

internal sealed class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--apply-update")
            return AppUpdateApplier.Run(args);

        // If an updater is replacing the application files, a shortcut click must not start a
        // competing instance that locks the executable halfway through the replacement.
        using (var updateProbe = new Mutex(false, AppUpdateApplier.UpdateMutexName))
        {
            if (!updateProbe.WaitOne(0)) return 0;
            updateProbe.ReleaseMutex();
        }
        using var instanceMutex = new Mutex(true, @"Local\NickFinancas.App", out var isFirstInstance);
        if (!isFirstInstance) return 0;

        var culture = CultureInfo.GetCultureInfo("pt-BR");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}
