param(
    [string]$ServerUrl = 'http://127.0.0.1:5210',
    [string]$DeviceName = 'Notebook Alex',
    [string]$ServerSettingsPath = "$env:ProgramFiles\Nick FinanÃ§as\Servidor\appsettings.Production.json",
    [string]$ClientSettingsPath = "$env:LOCALAPPDATA\NickFinancas\syncsettings.json"
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $ServerSettingsPath)) {
    throw "Configuracao do servidor nao encontrada em $ServerSettingsPath."
}

$server = Get-Content -LiteralPath $ServerSettingsPath -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($server.FINANCE_SYNC_API_KEY) -or
    [string]::IsNullOrWhiteSpace($server.FinanceSpaceId)) {
    throw 'O servidor ainda nao possui chave ou identificador do espaco financeiro.'
}

$settings = @{
    ServerUrl = $ServerUrl.TrimEnd('/') + '/'
    ApiKey = $server.FINANCE_SYNC_API_KEY
    SpaceId = $server.FinanceSpaceId
    DeviceId = "windows-$($env:COMPUTERNAME.ToLowerInvariant())"
    DeviceName = $DeviceName
    IntervalSeconds = 60
} | ConvertTo-Json

$directory = Split-Path -Parent $ClientSettingsPath
New-Item -ItemType Directory -Path $directory -Force | Out-Null
[IO.File]::WriteAllText($ClientSettingsPath, $settings, [Text.UTF8Encoding]::new($false))
Write-Host "Cliente de sincronizacao configurado em: $ClientSettingsPath"
