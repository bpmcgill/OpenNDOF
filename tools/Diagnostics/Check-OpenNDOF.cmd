@echo off
rem ===========================================================================
rem Check-OpenNDOF - one-click diagnostics for an OpenNDOF support issue.
rem ---------------------------------------------------------------------------
rem WHO:      End users and maintainers. No admin rights needed.
rem HOW:      Double-click this file. A console opens and prints the results;
rem           a copy is also saved to %TEMP%\OpenNDOF-diag.txt.
rem NEXT:     Paste the console text into the GitHub issue. The script's own
rem           comments describe what every section means (see
rem           Check-OpenNDOF.ps1, which this launcher runs).
rem ---------------------------------------------------------------------------
rem Keep this file ASCII-only: it must run on any Windows machine, and the
rem Windows PowerShell 5.1 parser chokes on UTF-8 bytes without a BOM.
rem ===========================================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Check-OpenNDOF.ps1"
echo.
pause