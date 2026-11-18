using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService(options => options.ServiceName = "NickFinancasSync");
builder.Services.AddSingleton<SyncDatabase>();
builder.Services.AddHostedService<SyncBackupService>();
var app = builder.Build();

await app.Services.GetRequiredService<SyncDatabase>().InitializeAsync();

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/health")
    {
        await next();
        return;
    }

    var configuredKey = app.Configuration["FINANCE_SYNC_API_KEY"];
    var suppliedKey = context.Request.Headers["X-Finance-Api-Key"].ToString();
    if (string.IsNullOrWhiteSpace(configuredKey) || !SecureEquals(configuredKey, suppliedKey))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "Chave de sincronizacao ausente ou invalida." });
        return;
    }

    await next();
});

app.MapGet("/health", async (SyncDatabase database, CancellationToken cancellationToken) =>
{
    var databaseOnline = await database.CanConnectAsync(cancellationToken);
    return databaseOnline
        ? Results.Ok(new { status = "online", database = "sqlite", utc = DateTimeOffset.UtcNow })
        : Results.Json(new { status = "degraded", database = "offline", utc = DateTimeOffset.UtcNow }, statusCode: 503);
});

app.MapPost("/api/devices/register", async (RegisterDeviceRequest request, SyncDatabase database,
    CancellationToken cancellationToken) =>
{
    if (request.SpaceId == Guid.Empty || string.IsNullOrWhiteSpace(request.DeviceId) ||
        string.IsNullOrWhiteSpace(request.DeviceName))
        return Results.BadRequest(new { error = "Espaco, identificador e nome do dispositivo sao obrigatorios." });

    await database.RegisterDeviceAsync(request, cancellationToken);
    return Results.Ok(new { registered = true, serverUtc = DateTimeOffset.UtcNow });
});

app.MapPost("/api/sync/push", async (PushChangesRequest request, SyncDatabase database,
    CancellationToken cancellationToken) =>
{
    if (request.SpaceId == Guid.Empty || string.IsNullOrWhiteSpace(request.DeviceId))
        return Results.BadRequest(new { error = "Espaco e dispositivo sao obrigatorios." });
    if (request.Changes.Count > 500)
        return Results.BadRequest(new { error = "Cada lote aceita no maximo 500 alteracoes." });
    if (request.Changes.Any(change => change.ChangeId == Guid.Empty || change.EntityId == Guid.Empty ||
        string.IsNullOrWhiteSpace(change.EntityType) || change.Operation is not ("upsert" or "delete")))
        return Results.BadRequest(new { error = "O lote contem uma alteracao invalida." });

    return Results.Ok(await database.PushAsync(request, cancellationToken));
});

app.MapGet("/api/sync/pull", async (Guid spaceId, string deviceId, long afterSequence,
    int? limit, SyncDatabase database, CancellationToken cancellationToken) =>
{
    if (spaceId == Guid.Empty || string.IsNullOrWhiteSpace(deviceId) || afterSequence < 0)
        return Results.BadRequest(new { error = "Parametros de sincronizacao invalidos." });
    var safeLimit = Math.Clamp(limit ?? 200, 1, 500);
    return Results.Ok(await database.PullAsync(spaceId, deviceId, afterSequence, safeLimit, cancellationToken));
});

app.MapGet("/api/updates/latest", (string? currentVersion, IConfiguration configuration) =>
{
    var directory = ResolveUpdateDirectory(configuration);
    var manifestPath = Path.Combine(directory, "latest.json");
    if (!File.Exists(manifestPath)) return Results.NoContent();
    var manifest = JsonSerializer.Deserialize<UpdateManifest>(File.ReadAllText(manifestPath));
    if (manifest is null || string.IsNullOrWhiteSpace(manifest.Version) ||
        string.Equals(manifest.Version, currentVersion, StringComparison.OrdinalIgnoreCase))
        return Results.NoContent();
    var packagePath = SafeUpdatePackagePath(directory, manifest.FileName);
    if (!File.Exists(packagePath)) return Results.NoContent();
    return Results.Ok(new
    {
        manifest.Version,
        manifest.ReleaseNotes,
        manifest.Sha256,
        SizeBytes = new FileInfo(packagePath).Length,
        DownloadUrl = "/api/updates/download"
    });
});

app.MapGet("/api/updates/download", (IConfiguration configuration) =>
{
    var directory = ResolveUpdateDirectory(configuration);
    var manifestPath = Path.Combine(directory, "latest.json");
    if (!File.Exists(manifestPath)) return Results.NotFound();
    var manifest = JsonSerializer.Deserialize<UpdateManifest>(File.ReadAllText(manifestPath));
    if (manifest is null) return Results.NotFound();
    var packagePath = SafeUpdatePackagePath(directory, manifest.FileName);
    return File.Exists(packagePath)
        ? Results.File(packagePath, "application/zip", manifest.FileName, enableRangeProcessing: true)
        : Results.NotFound();
});

app.Run();

static bool SecureEquals(string expected, string supplied)
{
    var left = Encoding.UTF8.GetBytes(expected);
    var right = Encoding.UTF8.GetBytes(supplied);
    return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
}

static string ResolveUpdateDirectory(IConfiguration configuration)
{
    var configured = configuration["Updates:Directory"];
    if (!string.IsNullOrWhiteSpace(configured))
        return Path.GetFullPath(Environment.ExpandEnvironmentVariables(configured));
    return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Nick FinanÃ§as", "Updates");
}

static string SafeUpdatePackagePath(string directory, string fileName)
{
    if (string.IsNullOrWhiteSpace(fileName) || fileName != Path.GetFileName(fileName))
        throw new InvalidOperationException("Nome de pacote de atualizacao invalido.");
    var root = Path.GetFullPath(directory) + Path.DirectorySeparatorChar;
    var path = Path.GetFullPath(Path.Combine(directory, fileName));
    if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Pacote fora do diretorio de atualizacoes.");
    return path;
}

public sealed record RegisterDeviceRequest(Guid SpaceId, string DeviceId, string DeviceName);
public sealed record PushChangesRequest(Guid SpaceId, string DeviceId, IReadOnlyList<SyncChangeInput> Changes);
public sealed record SyncChangeInput(Guid ChangeId, string EntityType, Guid EntityId, string Operation,
    JsonElement Payload, DateTimeOffset ChangedAtUtc, long LocalVersion);
public sealed record PushChangesResult(int Accepted, int Duplicates, long LastSequence, DateTimeOffset ServerUtc);
public sealed record PullChangesResult(IReadOnlyList<SyncChangeOutput> Changes, long LastSequence,
    bool HasMore, DateTimeOffset ServerUtc);
public sealed record SyncChangeOutput(long Sequence, Guid ChangeId, string DeviceId, string EntityType,
    Guid EntityId, string Operation, JsonElement Payload, DateTimeOffset ChangedAtUtc, long LocalVersion);
public sealed record UpdateManifest(string Version, string FileName, string Sha256, string ReleaseNotes);

public sealed class SyncDatabase(IConfiguration configuration)
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public string DatabasePath { get; } = ResolvePath(configuration["SyncServer:DatabasePath"], "Data", "sync.db");
    public string BackupDirectory { get; } = ResolvePath(configuration["SyncServer:BackupDirectory"], "Backups");

    private string ConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = DatabasePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Cache = SqliteCacheMode.Shared,
        Pooling = true,
        DefaultTimeout = 15
    }.ToString();

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        Directory.CreateDirectory(BackupDirectory);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            PRAGMA foreign_keys = ON;
            PRAGMA busy_timeout = 15000;
            CREATE TABLE IF NOT EXISTS sync_devices (
                space_id TEXT NOT NULL,
                device_id TEXT NOT NULL,
                device_name TEXT NOT NULL,
                registered_at_utc TEXT NOT NULL,
                last_seen_at_utc TEXT NOT NULL,
                PRIMARY KEY (space_id, device_id)
            );
            CREATE TABLE IF NOT EXISTS sync_changes (
                sequence INTEGER PRIMARY KEY AUTOINCREMENT,
                change_id TEXT NOT NULL UNIQUE,
                space_id TEXT NOT NULL,
                device_id TEXT NOT NULL,
                entity_type TEXT NOT NULL,
                entity_id TEXT NOT NULL,
                operation TEXT NOT NULL CHECK (operation IN ('upsert', 'delete')),
                payload TEXT NOT NULL,
                changed_at_utc TEXT NOT NULL,
                local_version INTEGER NOT NULL,
                received_at_utc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_sync_changes_space_sequence
                ON sync_changes(space_id, sequence);
            CREATE INDEX IF NOT EXISTS ix_sync_changes_entity
                ON sync_changes(space_id, entity_type, entity_id, sequence DESC);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA quick_check;";
            return string.Equals((string?)await command.ExecuteScalarAsync(cancellationToken), "ok",
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task RegisterDeviceAsync(RegisterDeviceRequest request, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO sync_devices(space_id, device_id, device_name, registered_at_utc, last_seen_at_utc)
                VALUES ($spaceId, $deviceId, $deviceName, $now, $now)
                ON CONFLICT(space_id, device_id) DO UPDATE SET
                    device_name = excluded.device_name,
                    last_seen_at_utc = excluded.last_seen_at_utc;
                """;
            command.Parameters.AddWithValue("$spaceId", request.SpaceId.ToString("D"));
            command.Parameters.AddWithValue("$deviceId", request.DeviceId.Trim());
            command.Parameters.AddWithValue("$deviceName", request.DeviceName.Trim());
            command.Parameters.AddWithValue("$now", ToDatabase(DateTimeOffset.UtcNow));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<PushChangesResult> PushAsync(PushChangesRequest request, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            var accepted = 0;
            var duplicates = 0;
            long lastSequence = 0;
            foreach (var change in request.Changes)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = (SqliteTransaction)transaction;
                command.CommandText = """
                    INSERT INTO sync_changes(change_id, space_id, device_id, entity_type, entity_id,
                        operation, payload, changed_at_utc, local_version, received_at_utc)
                    VALUES ($changeId, $spaceId, $deviceId, $entityType, $entityId, $operation,
                        $payload, $changedAtUtc, $localVersion, $receivedAtUtc)
                    ON CONFLICT(change_id) DO NOTHING
                    RETURNING sequence;
                    """;
                command.Parameters.AddWithValue("$changeId", change.ChangeId.ToString("D"));
                command.Parameters.AddWithValue("$spaceId", request.SpaceId.ToString("D"));
                command.Parameters.AddWithValue("$deviceId", request.DeviceId.Trim());
                command.Parameters.AddWithValue("$entityType", change.EntityType.Trim());
                command.Parameters.AddWithValue("$entityId", change.EntityId.ToString("D"));
                command.Parameters.AddWithValue("$operation", change.Operation);
                command.Parameters.AddWithValue("$payload", change.Payload.GetRawText());
                command.Parameters.AddWithValue("$changedAtUtc", ToDatabase(change.ChangedAtUtc));
                command.Parameters.AddWithValue("$localVersion", change.LocalVersion);
                command.Parameters.AddWithValue("$receivedAtUtc", ToDatabase(DateTimeOffset.UtcNow));
                var result = await command.ExecuteScalarAsync(cancellationToken);
                if (result is long sequence)
                {
                    accepted++;
                    lastSequence = Math.Max(lastSequence, sequence);
                }
                else
                {
                    duplicates++;
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return new PushChangesResult(accepted, duplicates, lastSequence, DateTimeOffset.UtcNow);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<PullChangesResult> PullAsync(Guid spaceId, string deviceId, long afterSequence,
        int limit, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT sequence, change_id, device_id, entity_type, entity_id, operation, payload,
                   changed_at_utc, local_version
            FROM sync_changes
            WHERE space_id = $spaceId AND sequence > $afterSequence
            ORDER BY sequence
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$spaceId", spaceId.ToString("D"));
        command.Parameters.AddWithValue("$afterSequence", afterSequence);
        command.Parameters.AddWithValue("$limit", limit + 1);
        var changes = new List<SyncChangeOutput>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            using var json = JsonDocument.Parse(reader.GetString(6));
            changes.Add(new SyncChangeOutput(reader.GetInt64(0), Guid.Parse(reader.GetString(1)), reader.GetString(2),
                reader.GetString(3), Guid.Parse(reader.GetString(4)), reader.GetString(5), json.RootElement.Clone(),
                DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                reader.GetInt64(8)));
        }

        var hasMore = changes.Count > limit;
        if (hasMore) changes.RemoveAt(changes.Count - 1);
        var lastSequence = changes.Count == 0 ? afterSequence : changes[^1].Sequence;
        return new PullChangesResult(changes, lastSequence, hasMore, DateTimeOffset.UtcNow);
    }

    public async Task CreateBackupAsync(string destinationPath, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            await using var source = await OpenAsync(cancellationToken);
            await using var destination = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = destinationPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false
            }.ToString());
            await destination.OpenAsync(cancellationToken);
            source.BackupDatabase(destination);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 15000;";
        await command.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }

    private static string ResolvePath(string? configuredPath, params string[] childParts)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredPath));

        var root = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return Path.Combine([root, "Nick FinanÃ§as", .. childParts]);
    }

    private static string ToDatabase(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}

public sealed class SyncBackupService(
    SyncDatabase database,
    IConfiguration configuration,
    ILogger<SyncBackupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalHours = Math.Clamp(configuration.GetValue("SyncServer:BackupIntervalHours", 24), 1, 168);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var path = Path.Combine(database.BackupDirectory,
                    $"sync-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.db");
                await database.CreateBackupAsync(path, stoppingToken);
                DeleteExpiredBackups(database.BackupDirectory,
                    Math.Clamp(configuration.GetValue("SyncServer:BackupRetentionCount", 30), 1, 365));
                logger.LogInformation("Backup SQLite criado em {BackupPath}", path);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Falha ao criar o backup SQLite do servidor de sincronizacao.");
            }

            await Task.Delay(TimeSpan.FromHours(intervalHours), stoppingToken);
        }
    }

    private static void DeleteExpiredBackups(string directory, int retentionCount)
    {
        foreach (var file in new DirectoryInfo(directory).GetFiles("sync-*.db")
                     .OrderByDescending(file => file.CreationTimeUtc).Skip(retentionCount))
            file.Delete();
    }
}
