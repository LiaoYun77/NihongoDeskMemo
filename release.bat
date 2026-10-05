@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\package.ps1" -Build
exit /b %ERRORLEVEL%
