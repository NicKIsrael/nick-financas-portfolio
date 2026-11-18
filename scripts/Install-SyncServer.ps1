param(
    [Parameter(Mandatory)]
    [string]$PublishPath,
    [string]$InstallPath = "$env:ProgramFiles\Nick FinanÃ§as\Servidor",
    [string]$DataRoot = "$env:ProgramData\Nick FinanÃ§as",
    [ValidateRange(1, 65535)]
    [int]$Port = 5210,
    [string]$ListenAddress = '127.0.0.1',
    [string]$ApiKey
)

$ErrorActionPreference = 'Stop'
$serviceName = 'NickFinancasSync'
$executableName = 'NickFinancas.SyncApi.exe'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Execute este instalador em um PowerShell aberto como Administrador.'
}

$publishFullPath = (Resolve-Path -LiteralPath $PublishPath).Path
$sourceExecutable = Join-Path $publishFullPath $executableName
if (-not (Test-Path -LiteralPath $sourceExecutable)) {
    throw "Executavel nao encontrado em $sourceExecutable. Execute Publish-SyncServer.ps1 primeiro."
}

$existingSettingsPath = Join-Path $InstallPath 'appsettings.Production.json'
$financeSpaceId = $null
if ([string]::IsNullOrWhiteSpace($ApiKey) -and (Test-Path -LiteralPath $existingSettingsPath)) {
    $existingSettings = Get-Content -LiteralPath $existingSettingsPath -Raw | ConvertFrom-Json
    $ApiKey = $existingSettings.FINANCE_SYNC_API_KEY
    $financeSpaceId = $existingSettings.FinanceSpaceId
}
if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    $bytes = [byte[]]::new(32)
    [Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    $ApiKey = [Convert]::ToBase64String($bytes)
}
if ([string]::IsNullOrWhiteSpace($financeSpaceId)) {
    $financeSpaceId = [guid]::NewGuid().ToString('D')
}

$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existing) {
    if ($existing.Status -ne 'Stopped') {
        Stop-Service -Name $serviceName -Force
        $existing.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
    }
    sc.exe delete $serviceName | Out-Null
    Start-Sleep -Seconds 2
}

New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $DataRoot 'Data') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $DataRoot 'Backups') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $DataRoot 'Updates') | Out-Null
Copy-Item -Path (Join-Path $publishFullPath '*') -Destination $InstallPath -Recurse -Force

$settings = @{
    FINANCE_SYNC_API_KEY = $ApiKey
    FinanceSpaceId = $financeSpaceId
    SyncServer = @{
        DatabasePath = (Join-Path $DataRoot 'Data\sync.db')
        BackupDirectory = (Join-Path $DataRoot 'Backups')
        BackupIntervalHours = 24
        BackupRetentionCount = 30
    }
} | ConvertTo-Json -Depth 4
$settingsPath = Join-Path $InstallPath 'appsettings.Production.json'
[IO.File]::WriteAllText($settingsPath, $settings, [Text.UTF8Encoding]::new($false))

icacls $DataRoot /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' | Out-Null
icacls $settingsPath /inheritance:r /grant:r '*S-1-5-18:F' '*S-1-5-32-544:F' | Out-Null

$executablePath = Join-Path $InstallPath $executableName
$binaryPath = '"{0}" --urls http://{1}:{2}' -f $executablePath, $ListenAddress, $Port
New-Service -Name $serviceName -BinaryPathName $binaryPath -DisplayName 'Nick FinanÃ§as - Sincronizacao' `
    -Description 'API leve de sincronizacao financeira com SQLite.' -StartupType Automatic
sc.exe config $serviceName start= delayed-auto | Out-Null
sc.exe failure $serviceName reset= 86400 actions= restart/5000/restart/15000/restart/60000 | Out-Null

Start-Service -Name $serviceName
(Get-Service -Name $serviceName).WaitForStatus('Running', [TimeSpan]::FromSeconds(30))
$health = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/health" -TimeoutSec 10
if ($health.status -ne 'online') {
    throw 'O servico iniciou, mas a verificacao de saude nao retornou online.'
}

Write-Host 'Servidor instalado e validado.'
Write-Host "Endereco local: http://127.0.0.1:$Port"
Write-Host "Banco: $(Join-Path $DataRoot 'Data\sync.db')"
Write-Host "Chave da API: $ApiKey"
Write-Warning 'Guarde a chave da API em local seguro. Ela sera necessaria para configurar os aplicativos.'
