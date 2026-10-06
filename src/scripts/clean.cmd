@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0clean.ps1"
set "CLEAN_EXIT_CODE=%ERRORLEVEL%"
if /i not "%~1"=="--no-pause" pause
exit /b %CLEAN_EXIT_CODE%
