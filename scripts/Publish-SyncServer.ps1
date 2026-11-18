param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\artifacts\sync-server-win-x64')
)

$ErrorActionPreference = 'Stop'
$projectPath = Join-Path $PSScriptRoot '..\src\NickFinancas.SyncApi\NickFinancas.SyncApi.csproj'

dotnet publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $OutputPath

if ($LASTEXITCODE -ne 0) {
    throw "A publicacao do servidor falhou com o codigo $LASTEXITCODE."
}

Write-Host "Servidor publicado em: $OutputPath"
