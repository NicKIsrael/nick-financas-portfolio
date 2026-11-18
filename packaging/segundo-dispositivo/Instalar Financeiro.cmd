@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Instalar-NickFinancas.ps1"
if errorlevel 1 pause

