param(
    [string]$SourceDirectory = (Join-Path $PSScriptRoot '..\artifacts\updates'),
    [string]$ServerUpdateDirectory = "$env:ProgramData\Nick FinanÃ§as\Updates"
)

$ErrorActionPreference = 'Stop'
$manifestPath = Join-Path $SourceDirectory 'latest.json'
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw 'Manifesto latest.json nao encontrado. Execute Publish-DesktopUpdate.ps1 primeiro.'
}
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$packagePath = Join-Path $SourceDirectory $manifest.FileName
if (-not (Test-Path -LiteralPath $packagePath)) { throw "Pacote nao encontrado: $packagePath" }
$actualHash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash
if ($actualHash -ne $manifest.Sha256) { throw 'O hash do pacote nao corresponde ao manifesto.' }

New-Item -ItemType Directory -Path $ServerUpdateDirectory -Force | Out-Null
Copy-Item -LiteralPath $packagePath -Destination $ServerUpdateDirectory -Force
Copy-Item -LiteralPath $manifestPath -Destination $ServerUpdateDirectory -Force
icacls $ServerUpdateDirectory /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' | Out-Null
Write-Host "Versao $($manifest.Version) disponibilizada no servidor."
