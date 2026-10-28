@echo off
rem Double-click to collect OpenNDOF diagnostics for a GitHub issue.
rem No admin required. The same output is saved to %%TEMP%%\OpenNDOF-diag.txt
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Check-OpenNDOF.ps1"
echo.
pause