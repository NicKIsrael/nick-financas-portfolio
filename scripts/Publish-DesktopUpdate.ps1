param(
    [Parameter(Mandatory)] [string]$Version,
    [string]$ReleaseNotes = 'Melhorias e correcoes do Nick FinanÃ§as.',
    [string]$OutputRoot = (Join-Path $PSScriptRoot '..\artifacts\updates')
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\src\NickFinancas.Desktop\NickFinancas.Desktop.csproj'
$publishDirectory = Join-Path $OutputRoot "Financeiro-$Version-win-x64"
$packageName = "Financeiro-$Version-win-x64.zip"
$packagePath = Join-Path $OutputRoot $packageName
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null

dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:Version=$Version -p:InformationalVersion=$Version -o $publishDirectory
if ($LASTEXITCODE -ne 0) { throw "Publicacao falhou com o codigo $LASTEXITCODE." }

Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $packagePath `
    -CompressionLevel Optimal -Force
$hash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash
$manifest = @{
    Version = $Version
    FileName = $packageName
    Sha256 = $hash
    ReleaseNotes = $ReleaseNotes
} | ConvertTo-Json
[IO.File]::WriteAllText((Join-Path $OutputRoot 'latest.json'), $manifest, [Text.UTF8Encoding]::new($false))

& (Join-Path $PSScriptRoot 'Update-DesktopShortcut.ps1') `
    -ExecutablePath (Join-Path $publishDirectory 'NickFinancas.exe')

Write-Host "Atualizacao publicada em: $packagePath"
Write-Host "Manifesto: $(Join-Path $OutputRoot 'latest.json')"
