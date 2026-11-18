$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework

try {
    $packageRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
    $source = Join-Path $packageRoot 'App'
    $installDirectory = Join-Path $env:LOCALAPPDATA 'Programs\Nick FinanÃ§as'
    $settingsDirectory = Join-Path $env:LOCALAPPDATA 'NickFinancas'
    $settingsSource = Join-Path $packageRoot 'syncsettings.segundo.json'
    $settingsDestination = Join-Path $settingsDirectory 'syncsettings.json'

    if (-not (Test-Path -LiteralPath (Join-Path $source 'NickFinancas.exe'))) {
        throw 'Os arquivos do aplicativo nao foram encontrados. Extraia todo o ZIP antes de instalar.'
    }

    Get-Process -Name 'NickFinancas' -ErrorAction SilentlyContinue | Stop-Process -Force
    New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null
    Copy-Item -Path (Join-Path $source '*') -Destination $installDirectory -Recurse -Force

    if (Test-Path -LiteralPath $settingsSource) {
        New-Item -ItemType Directory -Path $settingsDirectory -Force | Out-Null
        Copy-Item -LiteralPath $settingsSource -Destination $settingsDestination -Force
    }

    $executable = Join-Path $installDirectory 'NickFinancas.exe'
    $desktop = [Environment]::GetFolderPath('Desktop')
    $shortcutPath = Join-Path $desktop 'Nick FinanÃ§as.lnk'
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $executable
    $shortcut.WorkingDirectory = $installDirectory
    $shortcut.IconLocation = "$executable,0"
    $shortcut.Description = 'Nick FinanÃ§as'
    $shortcut.Save()

    Start-Process -FilePath $executable -WorkingDirectory $installDirectory
    [System.Windows.MessageBox]::Show(
        'Instalacao concluida. O atalho foi criado na Area de Trabalho.',
        'Nick FinanÃ§as') | Out-Null
}
catch {
    [System.Windows.MessageBox]::Show($_.Exception.Message, 'Erro na instalacao') | Out-Null
    exit 1
}

