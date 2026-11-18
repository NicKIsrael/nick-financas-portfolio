param(
    [Parameter(Mandatory)] [string]$Version,
    [string]$OutputRoot = (Join-Path $PSScriptRoot '..\artifacts'),
    [string]$ClientSettingsPath = "$env:LOCALAPPDATA\NickFinancas\syncsettings.json"
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\src\NickFinancas.Desktop\NickFinancas.Desktop.csproj'
$installerSource = Join-Path $PSScriptRoot '..\packaging\segundo-dispositivo'
$packageDirectory = Join-Path $OutputRoot "Nick-Financas-$Version-Instalador-Segundo-Dispositivo"
$appDirectory = Join-Path $packageDirectory 'App'
$packagePath = "$packageDirectory.zip"

if (-not (Test-Path -LiteralPath $ClientSettingsPath)) {
    throw 'A configuracao local de sincronizacao nao foi encontrada.'
}

New-Item -ItemType Directory -Path $appDirectory -Force | Out-Null
dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:Version=$Version -p:InformationalVersion=$Version -o $appDirectory
if ($LASTEXITCODE -ne 0) { throw "Publicacao falhou com o codigo $LASTEXITCODE." }

Copy-Item -Path (Join-Path $installerSource '*') -Destination $packageDirectory -Force
$settings = Get-Content -LiteralPath $ClientSettingsPath -Raw | ConvertFrom-Json
$settings.DeviceId = ''
$settings.DeviceName = 'Segundo computador'
$settings | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $packageDirectory 'syncsettings.segundo.json') -Encoding utf8NoBOM

$instructions = @'
NICK FINANÃAS

1. Extraia todo o conteudo deste ZIP para uma pasta.
2. Abra "Instalar Financeiro.cmd".
3. Ao terminar, use o atalho criado na Area de Trabalho.

O aplicativo ja inclui tudo o que precisa para funcionar. Nao e necessario instalar o .NET.
Para sincronizar fora da rede local, o Tailscale deve estar conectado a conta pessoal combinada com Alex.
'@
[IO.File]::WriteAllText((Join-Path $packageDirectory 'LEIA-ME.txt'), $instructions, [Text.UTF8Encoding]::new($false))

Compress-Archive -Path (Join-Path $packageDirectory '*') -DestinationPath $packagePath -CompressionLevel Optimal -Force
Write-Host "Instalador da Bia criado em: $packagePath"
Write-Host 'Este pacote contem a configuracao privada de sincronizacao; envie somente para a Bia.'

