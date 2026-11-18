using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NickFinancas.Infrastructure.Updates;

public sealed class AppUpdateService(Uri serverUrl, string apiKey)
{
    public async Task<PendingAppUpdate?> CheckAndDownloadAsync(
        string currentVersion,
        CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { BaseAddress = serverUrl, Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.Add("X-Finance-Api-Key", apiKey);
        using var response = await client.GetAsync(
            $"api/updates/latest?currentVersion={Uri.EscapeDataString(currentVersion)}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        response.EnsureSuccessStatusCode();
        var update = await response.Content.ReadFromJsonAsync<AvailableUpdate>(
            FinanceSyncJson.Options, cancellationToken)
            ?? throw new InvalidOperationException("Manifesto de atualizacao vazio.");
        if (!IsNewer(update.Version, currentVersion)) return null;

        var safeVersion = Regex.Replace(update.Version, "[^0-9A-Za-z._-]", "-");
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NickFinancas", "Updates");
        Directory.CreateDirectory(directory);
        var packagePath = Path.Combine(directory, $"Financeiro-{safeVersion}.zip");
        var temporaryPath = packagePath + ".download";

        using (var download = await client.GetAsync(update.DownloadUrl,
                   HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            download.EnsureSuccessStatusCode();
            await using var source = await download.Content.ReadAsStreamAsync(cancellationToken);
            await using var destination = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write,
                FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await source.CopyToAsync(destination, cancellationToken);
        }

        string actualHash;
        await using (var packageStream = File.OpenRead(temporaryPath))
            actualHash = Convert.ToHexString(await SHA256.HashDataAsync(packageStream, cancellationToken));
        if (!string.Equals(actualHash, update.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(temporaryPath);
            throw new InvalidDataException("A assinatura SHA-256 do pacote de atualizacao nao confere.");
        }
        File.Move(temporaryPath, packagePath, true);
        return new PendingAppUpdate(update.Version, packagePath, update.ReleaseNotes);
    }

    private static bool IsNewer(string candidate, string current)
    {
        var candidateParts = Regex.Matches(candidate, @"\d+").Select(match => int.Parse(match.Value)).ToArray();
        var currentParts = Regex.Matches(current, @"\d+").Select(match => int.Parse(match.Value)).ToArray();
        for (var index = 0; index < Math.Max(candidateParts.Length, currentParts.Length); index++)
        {
            var left = index < candidateParts.Length ? candidateParts[index] : 0;
            var right = index < currentParts.Length ? currentParts[index] : 0;
            if (left != right) return left > right;
        }
        return false;
    }

    private sealed record AvailableUpdate(
        string Version, string ReleaseNotes, string Sha256, long SizeBytes, string DownloadUrl);
}

public sealed record PendingAppUpdate(string Version, string PackagePath, string ReleaseNotes);

internal static class FinanceSyncJson
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
