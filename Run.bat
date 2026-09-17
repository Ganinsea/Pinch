@echo off
setlocal
if not exist "%~dp0bin\Pinch.exe" (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1"
)
start "" "%~dp0bin\Pinch.exe"
