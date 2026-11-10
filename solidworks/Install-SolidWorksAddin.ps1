# ===========================================================================
# Install-SolidWorksAddin.ps1
# ---------------------------------------------------------------------------
# Installs the EXPERIMENTAL OpenNDOF SOLIDWORKS add-in for the current user.
# Files go to %LOCALAPPDATA%\OpenNDOF\SolidWorks and the COM class is
# registered under HKCU. One key needs administrator rights (a UAC prompt is
# shown): SOLIDWORKS lists add-ins registered under HKCU\...\SolidWorks\Addins
# but only LOADS AT STARTUP those under HKLM\SOFTWARE\SolidWorks\Addins
# (measured on SOLIDWORKS 2026 SP4.1).
#
#   1. Build it first (SOLIDWORKS must be installed, for its interop DLLs):
#        dotnet build solidworks\OpenNDOF.SolidWorks -c Release
#   2. Close SOLIDWORKS (it locks the DLL), then run:
#        powershell -ExecutionPolicy Bypass -File solidworks\Install-SolidWorksAddin.ps1
#   3. Start SOLIDWORKS. Tools > Add-Ins lists "OpenNDOF SpaceMouse".
#
#   -Uninstall  removes the registration (the install folder is kept, so
#               your SpaceMouse.cfg survives).
#
# Keep this file ASCII-only: Windows PowerShell 5.1 misreads UTF-8 without BOM.
# ===========================================================================
param(
    [switch]$Uninstall,
    [string]$Source = (Join-Path $PSScriptRoot 'OpenNDOF.SolidWorks\bin\Release\net48')
)
$ErrorActionPreference = 'Stop'

$guid   = '{41726e6e-bf05-4139-a6bf-cacd88efff1b}'   # SpaceMouseAddin.AddinGuid
$class  = 'OpenNDOF.SolidWorks.SpaceMouseAddin'
$progId = 'OpenNDOF.SolidWorksAddin'
$title  = 'OpenNDOF SpaceMouse'
$desc   = '3Dconnexion device support via OpenNDOF (experimental)'
$dest   = Join-Path $env:LOCALAPPDATA 'OpenNDOF\SolidWorks'

$clsidKey   = "HKCU:\Software\Classes\CLSID\$guid"
$progIdKey  = "HKCU:\Software\Classes\$progId"
$addinKey   = "HKLM:\SOFTWARE\SolidWorks\Addins\$guid"
$startupKey = "HKCU:\Software\SolidWorks\AddInsStartup\$guid"
# Left by the pre-release prototype (same CLSID); removed on install/uninstall.
$legacyKeys = @('HKCU:\Software\Classes\SwSpaceMouse.Addin', "HKCU:\Software\SolidWorks\Addins\$guid")

function Remove-Keys([string[]]$keys) {
    foreach ($k in $keys) { if (Test-Path $k) { Remove-Item $k -Recurse -Force } }
}

# Runs a script block as administrator: directly when already elevated,
# otherwise in an elevated child PowerShell (one UAC prompt).
function Invoke-Elevated([scriptblock]$block) {
    $principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        & $block
        return
    }
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($block.ToString()))
    $p = Start-Process powershell -Verb RunAs -Wait -PassThru `
        -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-EncodedCommand', $encoded
    if ($p.ExitCode -ne 0) { throw "Elevated step failed (exit code $($p.ExitCode))." }
}

if ($Uninstall) {
    Remove-Keys (@($clsidKey, $progIdKey, $startupKey) + $legacyKeys)
    if (Test-Path $addinKey) {
        Invoke-Elevated ([scriptblock]::Create("Remove-Item '$addinKey' -Recurse -Force"))
    }
    Write-Host "OpenNDOF SOLIDWORKS add-in unregistered ($dest kept)."
    return
}

$built = Join-Path $Source 'OpenNDOF.SolidWorks.dll'
if (-not (Test-Path $built)) {
    throw "Add-in not built: $built not found. Run: dotnet build solidworks\OpenNDOF.SolidWorks -c Release"
}
if (Get-Process SLDWORKS -ErrorAction SilentlyContinue) {
    throw 'Close SOLIDWORKS first: it locks the add-in DLL.'
}

# --- files -----------------------------------------------------------------
New-Item -ItemType Directory -Force $dest | Out-Null
Copy-Item (Join-Path $Source '*.dll') $dest -Force
$cfg = Join-Path $dest 'SpaceMouse.cfg'
if (-not (Test-Path $cfg)) { Copy-Item (Join-Path $Source 'SpaceMouse.cfg') $cfg }

# --- COM registration (per user, .NET Framework via mscoree) ---------------
$dll = Join-Path $dest 'OpenNDOF.SolidWorks.dll'
$assembly = [Reflection.AssemblyName]::GetAssemblyName($dll).FullName
Remove-Keys (@($clsidKey) + $legacyKeys)

New-Item -Force $clsidKey -Value $class | Out-Null
New-Item -Force "$clsidKey\ProgId" -Value $progId | Out-Null
# CATID_.NET Category
New-Item -Force "$clsidKey\Implemented Categories\{62C8FE65-4EBB-45e7-B440-6E39B2CDBF29}" | Out-Null
$inproc = New-Item -Force "$clsidKey\InprocServer32" -Value 'mscoree.dll'
$values = [ordered]@{
    ThreadingModel = 'Both'
    Class          = $class
    Assembly       = $assembly
    RuntimeVersion = 'v4.0.30319'
    CodeBase       = ([Uri]$dll).AbsoluteUri
}
foreach ($v in $values.GetEnumerator()) {
    New-ItemProperty -Path $inproc.PSPath -Name $v.Key -Value $v.Value -Force | Out-Null
}
New-Item -Force $progIdKey -Value $class | Out-Null
New-Item -Force "$progIdKey\CLSID" -Value $guid | Out-Null

# --- SOLIDWORKS add-in list (HKLM, admin) + load at startup (HKCU) ----------
Invoke-Elevated ([scriptblock]::Create(@"
`$ErrorActionPreference = 'Stop'
`$k = New-Item -Force '$addinKey'
Set-ItemProperty -Path `$k.PSPath -Name '(default)' -Value 1 -Type DWord
New-ItemProperty -Path `$k.PSPath -Name Title -Value '$title' -Force | Out-Null
New-ItemProperty -Path `$k.PSPath -Name Description -Value '$desc' -Force | Out-Null
"@))
$startup = New-Item -Force $startupKey
Set-ItemProperty -Path $startup.PSPath -Name '(default)' -Value 1 -Type DWord

Write-Host "OpenNDOF SOLIDWORKS add-in installed: $dll"
Write-Host "Settings: $cfg   Log: $(Join-Path $dest 'OpenNDOF.SolidWorks.log')"
