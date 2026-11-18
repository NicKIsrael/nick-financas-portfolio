using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace NickFinancas.Desktop.Services;

internal static class AppUpdateApplier
{
    internal const string UpdateMutexName = @"Local\NickFinancas.Update";

    public static int Run(string[] args)
    {
        try
        {
            using var updateMutex = new Mutex(true, UpdateMutexName, out var ownsUpdateMutex);
            if (!ownsUpdateMutex) return 0;

            var values = ParseArguments(args);
            var packagePath = Path.GetFullPath(values["package"]);
            var targetDirectory = Path.GetFullPath(values["target"]);
            var executableName = Path.GetFileName(values["executable"]);
            var processId = int.Parse(values["pid"]);
            if (!File.Exists(packagePath) || string.IsNullOrWhiteSpace(executableName) ||
                Path.GetPathRoot(targetDirectory)?.TrimEnd(Path.DirectorySeparatorChar) ==
                targetDirectory.TrimEnd(Path.DirectorySeparatorChar))
                return 2;

            try { Process.GetProcessById(processId).WaitForExit(60000); }
            catch (ArgumentException) { }
            catch (System.ComponentModel.Win32Exception) { }

            var updateRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NickFinancas", "Updates");
            var staging = Path.Combine(updateRoot, "staging-" + Guid.NewGuid().ToString("N"));
            var backup = Path.Combine(updateRoot, "previous");
            Directory.CreateDirectory(staging);
            ZipFile.ExtractToDirectory(packagePath, staging, true);

            if (Directory.Exists(backup)) Directory.Delete(backup, true);
            Directory.CreateDirectory(backup);
            foreach (var file in Directory.GetFiles(targetDirectory))
                File.Copy(file, Path.Combine(backup, Path.GetFileName(file)), true);

            try
            {
                foreach (var file in Directory.GetFiles(staging, "*", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(staging, file);
                    var destination = Path.Combine(targetDirectory, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    CopyWithRetry(file, destination);
                }
            }
            catch
            {
                foreach (var file in Directory.GetFiles(backup))
                    File.Copy(file, Path.Combine(targetDirectory, Path.GetFileName(file)), true);
                throw;
            }
            finally
            {
                Directory.Delete(staging, true);
            }

            if (OperatingSystem.IsWindows())
                SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);

            // The freshly updated application probes this mutex before starting. Release it
            // before launching the new process; otherwise the updater itself makes the restart
            // look like a competing start and the application exits without showing a window.
            updateMutex.ReleaseMutex();
            Process.Start(new ProcessStartInfo(Path.Combine(targetDirectory, executableName))
            {
                UseShellExecute = true,
                WorkingDirectory = targetDirectory,
            });
            return 0;
        }
        catch (Exception exception)
        {
            try
            {
                var logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NickFinancas", "Updates");
                Directory.CreateDirectory(logDirectory);
                File.WriteAllText(Path.Combine(logDirectory, "updater-error.log"), exception.ToString());
            }
            catch
            {
                // O codigo de saida ainda informa a falha quando nem mesmo o log pode ser gravado.
            }
            return 1;
        }
    }

    private static void CopyWithRetry(string source, string destination)
    {
        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (true)
        {
            try
            {
                File.Copy(source, destination, true);
                return;
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(500);
            }
            catch (UnauthorizedAccessException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(500);
            }
        }
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);

    private static Dictionary<string, string> ParseArguments(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index + 1 < args.Length; index += 2)
            result[args[index].TrimStart('-')] = args[index + 1];
        foreach (var required in new[] { "package", "target", "pid", "executable" })
            if (!result.ContainsKey(required)) throw new ArgumentException($"Argumento ausente: {required}");
        return result;
    }
}
