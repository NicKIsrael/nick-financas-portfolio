param(
    [switch]$RemoveData,
    [string]$InstallPath = "$env:ProgramFiles\Nick FinanÃ§as\Servidor",
    [string]$DataRoot = "$env:ProgramData\Nick FinanÃ§as"
)

$ErrorActionPreference = 'Stop'
$serviceName = 'NickFinancasSync'
$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($service) {
    if ($service.Status -ne 'Stopped') {
        Stop-Service -Name $serviceName -Force
        $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
    }
    sc.exe delete $serviceName | Out-Null
}

if (Test-Path -LiteralPath $InstallPath) {
    Remove-Item -LiteralPath $InstallPath -Recurse -Force
}

if ($RemoveData -and (Test-Path -LiteralPath $DataRoot)) {
    Remove-Item -LiteralPath $DataRoot -Recurse -Force
}

Write-Host 'Servico removido.'
if (-not $RemoveData) {
    Write-Host "Banco e backups preservados em: $DataRoot"
}
