# ===========================================================================
# Check-OpenNDOF.ps1
# ---------------------------------------------------------------------------
# One-paste diagnostics for OpenNDOF support issues.
#
# WHO IS THIS FOR?
#   Non-technical users and maintainers alike. Double-click
#   Check-OpenNDOF.cmd (or run this file), then paste the console output
#   into a GitHub issue. No administrator rights are required.
#
# WHAT IT ANSWERS (read the section headers in the output):
#   Section 1 - 3Dconnexion / 3DxWare driver:
#       Is the official driver (or any leftover piece of it) installed?
#       SolidWorks and Fusion gate their 3Dconnexion support on this driver.
#       "NOT DETECTED" means there is nothing to conflict with OpenNDOF.
#   Section 2 - OpenNDOF COM registration:
#       Does Windows know OpenNDOF's TDxInput.Device, and does the
#       comhost DLL it points at actually exist? If "NOT registered" or
#       "exists: False", the fix is to run the OpenNDOF installer again.
#   Section 3 - Activation trace:
#       %TEMP%\OpenNDOF-trace.log is appended to every time a program
#       opens OpenNDOF's COM component. AFTER starting a CAD app:
#       - trace has new lines  => the app DID open OpenNDOF (problem is
#         elsewhere, e.g. events or the app's own settings)
#       - trace is empty/missing => the app never opened OpenNDOF at all
#         (the app does not use the TDxInput COM API, or it is gated on
#         the 3DxWare driver). This is how SolidWorks was ruled out.
#   Section 4 - Connected 3Dconnexion HID devices:
#       What Logitech/3Dconnexion (vendor ID 046D) hardware Windows sees.
#       Look for PID C625 = SpacePilot, C627 = SpaceExplorer, C626 =
#       SpaceNavigator, C623 = SpaceTraveler.
#
# WHY ASCII ONLY?
#   This file must parse under Windows PowerShell 5.1, which misreads
#   UTF-8 bytes without a BOM; keep every character below 0x80.
# ===========================================================================

$out = Join-Path $env:TEMP 'OpenNDOF-diag.txt'
$lines = [System.Collections.Generic.List[string]]::new()
function Add-Line([string]$s) { $lines.Add($s); Write-Host $s }

# ---------------------------------------------------------------------------
# Section 1: detect the official 3Dconnexion driver / any remnants of it.
# ---------------------------------------------------------------------------
function Test-3DxWare() {
    Add-Line "== 1. 3Dconnexion / 3DxWare driver =="
    $hits = @()

    # Programs and Features entries (both 64-bit and 32-bit registry views)
    foreach ($root in @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*')) {
        Get-ItemProperty $root -ErrorAction SilentlyContinue |
            Where-Object { $_.DisplayName -match '3Dconnexion|3DxWare|3DxSoft' } |
            ForEach-Object { $hits += "PRODUCT: $($_.DisplayName) ($($_.DisplayVersion))" }
    }
    # Install directories left behind by the driver installer
    foreach ($d in @('C:\Program Files\3Dconnexion', 'C:\Program Files (x86)\3Dconnexion')) {
        if (Test-Path $d) { $hits += "INSTALL DIR: $d" }
    }
    # Registry vendor keys (driver configuration lives here when installed)
    foreach ($k in @('HKLM:\SOFTWARE\3Dconnexion', 'HKLM:\SOFTWARE\WOW6432Node\3Dconnexion')) {
        if (Test-Path $k) { $hits += "REGISTRY KEY: $k" }
    }
    # Driver services and any running 3Dconnexion processes
    Get-CimInstance Win32_Service -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '3dx' -or $_.DisplayName -match '3Dconnexion|3DxWare' } |
        ForEach-Object { $hits += "SERVICE: $($_.Name) ($($_.State))" }
    Get-Process -ErrorAction SilentlyContinue |
        Where-Object { $_.ProcessName -match '3dx|3dconnexion' } |
        ForEach-Object { $hits += "PROCESS: $($_.ProcessName)" }

    if ($hits.Count -eq 0) { Add-Line "  NOT DETECTED -- no 3Dconnexion/3DxWare driver present." }
    else { $hits | ForEach-Object { Add-Line "  FOUND: $_" } }
    Add-Line ""
}

# ---------------------------------------------------------------------------
# Section 2: is OpenNDOF's TDxInput.Device registered, and is the DLL there?
# ---------------------------------------------------------------------------
function Test-OpenNDOFRegistration() {
    Add-Line "== 2. OpenNDOF COM registration =="
    $clsid = 'HKLM:\SOFTWARE\Classes\CLSID\{82C5AB54-C92C-4D52-AAC5-27E25E22604C}\InprocServer32'
    $dll = (Get-ItemProperty $clsid -ErrorAction SilentlyContinue).'(default)'
    if ($dll) {
        Add-Line "  TDxInput.Device -> $dll"
        Add-Line "  comhost file exists: $(Test-Path $dll)"
    } else {
        Add-Line "  TDxInput.Device NOT registered. Re-run the OpenNDOF installer."
    }
    Add-Line ""
}

# ---------------------------------------------------------------------------
# Section 3: the activation trace -- the single most useful diagnostic.
# ---------------------------------------------------------------------------
function Test-Trace() {
    Add-Line "== 3. OpenNDOF activation trace =="
    $tr = Join-Path $env:TEMP 'OpenNDOF-trace.log'
    if (Test-Path $tr) {
        Add-Line "  trace exists; last lines:"
        Get-Content $tr | Select-Object -Last 15 | ForEach-Object { Add-Line "    $_" }
    } else {
        Add-Line "  No trace file yet -- nothing has activated the COM server."
        Add-Line "  TIP: run your CAD app, use it briefly, then re-run this script."
    }
    Add-Line ""
}

# ---------------------------------------------------------------------------
# Section 4: which 3Dconnexion hardware does Windows currently see?
# ---------------------------------------------------------------------------
function Test-Devices() {
    Add-Line "== 4. Connected 3Dconnexion HID devices =="
    $devs = Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue |
        Where-Object { $_.InstanceId -match 'VID_046D' }
    if ($devs) {
        $devs | ForEach-Object { Add-Line "  $($_.FriendlyName)  [$($_.InstanceId -replace '.*\\\\','')]" }
    } else {
        Add-Line "  No Logitech/3Dconnexion (VID 046D) devices present."
        Add-Line "  TIP: plug the SpaceMouse in and re-run."
    }
    Add-Line ""
}

Add-Line "OpenNDOF diagnostics -- $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
Add-Line "================================================================"
Add-Line ""
Test-3DxWare
Test-OpenNDOFRegistration
Test-Trace
Test-Devices
Add-Line "Done. Copy this whole text into the GitHub issue."
Add-Line ""
Add-Line "Short guide:"
Add-Line "  1) Nothing found in section 1 and section 4 shows your device"
Add-Line "     => clean machine, device visible; good starting point."
Add-Line "  2) Section 3 empty after running your CAD app"
Add-Line "     => the app never opened OpenNDOF (see the wiki for which apps"
Add-Line "        use the TDxInput COM API vs. the 3DxWare-gated path)."
Add-Line "  3) Section 2 'exists: False' => reinstall OpenNDOF."

$lines | Out-File -FilePath $out -Encoding utf8
Write-Host ""
Write-Host "A copy was saved to $out (paste that if the console text is long)."