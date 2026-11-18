using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NickFinancas.Infrastructure.Persistence;

namespace NickFinancas.Infrastructure.Sync;

public sealed record FinanceSyncOptions(
    Uri ServerUrl,
    string ApiKey,
    Guid SpaceId,
    string DeviceId,
    string DeviceName,
    TimeSpan Interval)
{
    public static FinanceSyncOptions? LoadDefault()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NickFinancas", "syncsettings.json");
        if (!File.Exists(path)) return null;
        var settings = JsonSerializer.Deserialize<SyncSettingsFile>(File.ReadAllText(path), JsonOptions);
        if (settings is null || !Uri.TryCreate(settings.ServerUrl, UriKind.Absolute, out var serverUrl) ||
            string.IsNullOrWhiteSpace(settings.ApiKey) || settings.SpaceId == Guid.Empty)
            return null;

        return new FinanceSyncOptions(serverUrl, settings.ApiKey, settings.SpaceId,
            string.IsNullOrWhiteSpace(settings.DeviceId) ? Environment.MachineName : settings.DeviceId,
            string.IsNullOrWhiteSpace(settings.DeviceName) ? Environment.MachineName : settings.DeviceName,
            TimeSpan.FromSeconds(Math.Clamp(settings.IntervalSeconds, 2, 300)));
    }

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record SyncSettingsFile(
        string ServerUrl,
        string ApiKey,
        Guid SpaceId,
        string DeviceId,
        string DeviceName,
        int IntervalSeconds = 60);
}

public sealed class FinanceSyncService : IAsyncDisposable
{
    private static readonly IReadOnlyDictionary<string, Type> EntityTypes = new Dictionary<string, Type>
    {
        [nameof(AccountRecord)] = typeof(AccountRecord),
        [nameof(CategoryRecord)] = typeof(CategoryRecord),
        [nameof(TransactionRecord)] = typeof(TransactionRecord),
        [nameof(CreditCardRecord)] = typeof(CreditCardRecord),
        [nameof(PayableRecord)] = typeof(PayableRecord),
        [nameof(RecurringPayableRecord)] = typeof(RecurringPayableRecord),
        [nameof(ReserveRecord)] = typeof(ReserveRecord),
        [nameof(ReserveMovementRecord)] = typeof(ReserveMovementRecord),
        [nameof(AccountBalanceCorrectionRecord)] = typeof(AccountBalanceCorrectionRecord),
        [nameof(GoalRecord)] = typeof(GoalRecord),
        [nameof(FinancialSpaceRecord)] = typeof(FinancialSpaceRecord),
        [nameof(MemberRecord)] = typeof(MemberRecord),
    };

    private readonly SqliteFinanceStore _store;
    private readonly FinanceSyncOptions _options;
    private readonly HttpClient _client;
    private readonly CancellationTokenSource _stopping = new();
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly SemaphoreSlim _wakeSignal = new(0, 1);
    private Task? _loop;

    public FinanceSyncService(SqliteFinanceStore store, FinanceSyncOptions options)
    {
        _store = store;
        _options = options;
        _client = new HttpClient { BaseAddress = options.ServerUrl, Timeout = TimeSpan.FromSeconds(15) };
        _client.DefaultRequestHeaders.Add("X-Finance-Api-Key", options.ApiKey);
        SqliteFinanceStore.LocalDataChanged += OnLocalDataChanged;
    }

    public event EventHandler? RemoteDataApplied;
    public event EventHandler<FinanceSyncStatus>? StatusChanged;

    // Keep network synchronization off the UI synchronization context. Capturing Avalonia's
    // context here made application shutdown wait on a continuation that could only run on the
    // thread currently performing that wait, leaving an invisible process holding the app mutex.
    public void Start() => _loop ??= Task.Run(() => RunLoopAsync(_stopping.Token));

    public async Task SynchronizeOnceAsync(CancellationToken cancellationToken = default)
    {
        if (!await _syncLock.WaitAsync(0, cancellationToken)) return;
        try
        {
            StatusChanged?.Invoke(this, new FinanceSyncStatus("syncing", null, DateTimeOffset.UtcNow));
            await RegisterDeviceAsync(cancellationToken);
            await PushAsync(cancellationToken);
            var applied = await PullAsync(cancellationToken);
            if (applied > 0)
                await _store.EnsurePrimaryAccessAsync(cancellationToken);
            StatusChanged?.Invoke(this, new FinanceSyncStatus("online", null, DateTimeOffset.UtcNow));
            if (applied > 0) RemoteDataApplied?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            StatusChanged?.Invoke(this, new FinanceSyncStatus("offline", exception.Message, DateTimeOffset.UtcNow));
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        SqliteFinanceStore.LocalDataChanged -= OnLocalDataChanged;
        await _stopping.CancelAsync();
        if (_loop is not null)
        {
            try { await _loop; }
            catch (OperationCanceledException) { }
        }
        _client.Dispose();
        _stopping.Dispose();
        _syncLock.Dispose();
        _wakeSignal.Dispose();
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await SynchronizeOnceAsync(cancellationToken);
            await _wakeSignal.WaitAsync(_options.Interval, cancellationToken);
        }
    }

    private void OnLocalDataChanged(object? sender, EventArgs eventArgs)
    {
        if (_wakeSignal.CurrentCount == 0)
            _wakeSignal.Release();
    }

    private async Task RegisterDeviceAsync(CancellationToken cancellationToken)
    {
        var response = await _client.PostAsJsonAsync("api/devices/register",
            new { _options.SpaceId, _options.DeviceId, _options.DeviceName }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task PushAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            await using var database = new LocalFinanceDbContext(_store.SyncOptions);
            var pending = await database.SyncOutbox.AsNoTracking()
                .OrderBy(item => item.LocalVersion).ThenBy(item => item.ChangeId)
                .Take(200).ToListAsync(cancellationToken);
            if (pending.Count == 0) return;

            var changes = pending.Select(item => new
            {
                item.ChangeId,
                item.EntityType,
                item.EntityId,
                item.Operation,
                Payload = JsonSerializer.Deserialize<JsonElement>(item.Payload, FinanceSyncOptions.JsonOptions),
                item.ChangedAtUtc,
                item.LocalVersion,
            }).ToList();
            var response = await _client.PostAsJsonAsync("api/sync/push",
                new { _options.SpaceId, _options.DeviceId, Changes = changes },
                FinanceSyncOptions.JsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();

            database.SyncOutbox.RemoveRange(await database.SyncOutbox
                .Where(item => pending.Select(sent => sent.ChangeId).Contains(item.ChangeId))
                .ToListAsync(cancellationToken));
            await database.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<int> PullAsync(CancellationToken cancellationToken)
    {
        var applied = 0;
        while (true)
        {
            await using var database = new LocalFinanceDbContext(_store.SyncOptions) { SuppressSyncTracking = true };
            var state = await database.SyncState.SingleAsync(item => item.Id == 1, cancellationToken);
            var url = $"api/sync/pull?spaceId={_options.SpaceId:D}&deviceId={Uri.EscapeDataString(_options.DeviceId)}" +
                      $"&afterSequence={state.LastPulledSequence}&limit=200";
            var result = await _client.GetFromJsonAsync<PullResult>(url, FinanceSyncOptions.JsonOptions, cancellationToken)
                         ?? throw new InvalidOperationException("O servidor retornou uma resposta de sincronizacao vazia.");

            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            foreach (var change in result.Changes)
            {
                if (EntityTypes.TryGetValue(change.EntityType, out var entityType))
                    await ApplyChangeAsync(database, entityType, change, cancellationToken);
            }
            state.LastPulledSequence = result.LastSequence;
            state.LastSuccessfulSyncUtc = DateTimeOffset.UtcNow;
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            applied += result.Changes.Count;
            if (!result.HasMore) return applied;
        }
    }

    private static async Task ApplyChangeAsync(LocalFinanceDbContext database, Type entityType,
        RemoteChange change, CancellationToken cancellationToken)
    {
        var existing = await database.FindAsync(entityType, [change.EntityId], cancellationToken);
        if (change.Operation == "delete")
        {
            if (existing is not null) database.Remove(existing);
            return;
        }

        var incoming = JsonSerializer.Deserialize(change.Payload.GetRawText(), entityType,
            FinanceSyncOptions.JsonOptions) ?? throw new JsonException($"Payload invalido para {change.EntityType}.");
        if (existing is null)
            database.Add(incoming);
        else
            database.Entry(existing).CurrentValues.SetValues(incoming);
    }

    private sealed record PullResult(IReadOnlyList<RemoteChange> Changes, long LastSequence, bool HasMore);
    private sealed record RemoteChange(long Sequence, Guid ChangeId, string DeviceId, string EntityType,
        Guid EntityId, string Operation, JsonElement Payload, DateTimeOffset ChangedAtUtc, long LocalVersion);
}

public sealed record FinanceSyncStatus(string State, string? Error, DateTimeOffset ChangedAtUtc);
