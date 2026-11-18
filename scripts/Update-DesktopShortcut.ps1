param(
    [Parameter(Mandatory)] [string]$ExecutablePath,
    [string]$DesktopDirectory = [Environment]::GetFolderPath('Desktop')
)

$ErrorActionPreference = 'Stop'
$executable = (Resolve-Path -LiteralPath $ExecutablePath -ErrorAction Stop).Path
if ([IO.Path]::GetFileName($executable) -ne 'NickFinancas.exe') {
    throw "Executavel inesperado: $executable"
}

$shortcutPath = Join-Path $DesktopDirectory 'Nick FinanÃ§as.lnk'
$temporaryPath = Join-Path $DesktopDirectory "Nick FinanÃ§as.$([Guid]::NewGuid().ToString('N')).lnk"
$backupPath = Join-Path $DesktopDirectory "Nick FinanÃ§as.$([Guid]::NewGuid().ToString('N')).bak"
$shell = New-Object -ComObject WScript.Shell
try {
    $shortcut = $shell.CreateShortcut($temporaryPath)
    $shortcut.TargetPath = $executable
    $shortcut.Arguments = ''
    $shortcut.WorkingDirectory = Split-Path -Parent $executable
    $shortcut.IconLocation = "$executable,0"
    $shortcut.Description = 'Nick FinanÃ§as'
    $shortcut.Save()

    if ($shell.CreateShortcut($temporaryPath).TargetPath -ne $executable) {
        throw 'Falha ao criar o novo atalho.'
    }
    if (Test-Path -LiteralPath $shortcutPath) {
        Copy-Item -LiteralPath $shortcutPath -Destination $backupPath -Force
    }
    Copy-Item -LiteralPath $temporaryPath -Destination $shortcutPath -Force
    if ($shell.CreateShortcut($shortcutPath).TargetPath -ne $executable) {
        if (Test-Path -LiteralPath $backupPath) {
            Copy-Item -LiteralPath $backupPath -Destination $shortcutPath -Force
        }
        throw "Falha ao atualizar o atalho: $shortcutPath"
    }
}
finally {
    Remove-Item -LiteralPath $temporaryPath -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $backupPath -ErrorAction SilentlyContinue
}
Write-Host "Atalho atualizado: $shortcutPath -> $executable"
