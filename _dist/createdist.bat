@echo off
rem createdist.bat - convenience wrapper for createdist.ps1
rem Arguments are passed through, e.g.: createdist.bat -PlatformToolset v143

setlocal
set "PS=pwsh"
where pwsh >nul 2>nul || set "PS=powershell"

%PS% -NoProfile -ExecutionPolicy Bypass -File "%~dp0createdist.ps1" %*
exit /b %ERRORLEVEL%
