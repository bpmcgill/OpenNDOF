# Check-OpenNDOF.ps1
# One-paste diagnostics for the OpenNDOF issue template.
# Run via the bundled Check-OpenNDOF.cmd (double-click). No admin needed.
# Writes a copy to %TEMP%\OpenNDOF-diag.txt and prints everything to the console.

$out = Join-Path $env:TEMP 'OpenNDOF-diag.txt'
$lines = [System.Collections.Generic.List[string]]::new()
function Add-Line([string]$s) { $lines.Add($s); Write-Host $s }

function Test-3DxWare() {
    Add-Line "== 1. 3Dconnexion / 3DxWare driver =="
    $hits = @()

    # Programs and Features entries (64 + 32-bit views)
    foreach ($root in @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*')) {
        Get-ItemProperty $root -ErrorAction SilentlyContinue |
            Where-Object { $_.DisplayName -match '3Dconnexion|3DxWare|3DxSoft' } |
            ForEach-Object { $hits += "PRODUCT: $($_.DisplayName) ($($_.DisplayVersion))" }
    }
    # Install directories
    foreach ($d in @('C:\Program Files\3Dconnexion', 'C:\Program Files (x86)\3Dconnexion')) {
        if (Test-Path $d) { $hits += "INSTALL DIR: $d" }
    }
    # Registry vendor keys
    foreach ($k in @('HKLM:\SOFTWARE\3Dconnexion', 'HKLM:\SOFTWARE\WOW6432Node\3Dconnexion')) {
        if (Test-Path $k) { $hits += "REGISTRY KEY: $k" }
    }
    # Services and running processes
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

function Test-OpenNDOFRegistration() {
    Add-Line "== 2. OpenNDOF COM registration =="
    $clsid = 'HKLM:\SOFTWARE\Classes\CLSID\{82C5AB54-C92C-4D52-AAC5-27E25E22604C}\InprocServer32'
    $dll = (Get-ItemProperty $clsid -ErrorAction SilentlyContinue).'(default)'
    if ($dll) {
        Add-Line "  TDxInput.Device -> $dll"
        Add-Line "  comhost file exists: $(Test-Path $dll)"
    } else {
        Add-Line "  TDxInput.Device NOT registered."
    }
    Add-Line ""
}

function Test-Trace() {
    Add-Line "== 3. OpenNDOF activation trace =="
    $tr = Join-Path $env:TEMP 'OpenNDOF-trace.log'
    if (Test-Path $tr) {
        Add-Line "  trace exists; last lines:"
        Get-Content $tr | Select-Object -Last 15 | ForEach-Object { Add-Line "    $_" }
    } else {
        Add-Line "  No trace file yet -- nothing has activated the COM server."
    }
    Add-Line ""
}

function Test-Devices() {
    Add-Line "== 4. Connected 3Dconnexion HID devices =="
    $devs = Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue |
        Where-Object { $_.InstanceId -match 'VID_046D' }
    if ($devs) {
        $devs | ForEach-Object { Add-Line "  $($_.FriendlyName)  [$($_.InstanceId -replace '.*\\\\','')]" }
    } else {
        Add-Line "  No Logitech/3Dconnexion (VID 046D) devices present."
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

$lines | Out-File -FilePath $out -Encoding utf8
Write-Host ""
Write-Host "A copy was saved to $out (paste that if the console text is long)."