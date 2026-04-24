#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Registers or unregisters OpenNDOF as the TDxInput COM server.

.DESCRIPTION
    Applications that support 3DConnexion devices (AutoCAD, SolidWorks, Blender,
    Maya, etc.) call CoCreateInstance with ProgID "TDxInput.Device". This script
    points that lookup at OpenNDOF's TDxInput.dll instead of the official driver.

    The .NET COM host (comhost.dll, built alongside TDxInput.dll) handles
    DllGetClassObject and DllRegisterServer automatically.

.PARAMETER Unregister
    Remove the OpenNDOF COM registration and restore the system to its previous state.

.EXAMPLE
    .\Register-ComServer.ps1
    .\Register-ComServer.ps1 -Unregister
#>
param(
    [switch]$Unregister
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Locate build output ──────────────────────────────────────────────────────
$scriptDir  = Split-Path $MyInvocation.MyCommand.Path
$comhost    = Join-Path $scriptDir "TDxInput.comhost.dll"
$coredll    = Join-Path $scriptDir "TDxInput.dll"

if (-not (Test-Path $comhost)) {
    Write-Error "TDxInput.comhost.dll not found at: $comhost`nBuild the solution first (dotnet build src\OpenNDOF.Core)."
}

# ── CLSIDs that must be registered ───────────────────────────────────────────
$clsids = @(
    @{ Clsid = "{82C5AB54-C92C-4D52-AAC5-27E25E22604C}"; ProgId = "TDxInput.Device"   },
    @{ Clsid = "{85004B00-1AA7-4777-B1CE-8427301B942D}"; ProgId = "TDxInput.Sensor"   },
    @{ Clsid = "{25BBE090-583A-4903-A61B-D0EC629AC4EC}"; ProgId = "TDxInput.Keyboard" },
    @{ Clsid = "{1A960ECE-0E57-4A68-B694-8373114F1FF4}"; ProgId = "TDxInput.TDxInfo"  },
    @{ Clsid = "{512A6C3E-3010-401B-8623-E413E2ACC138}"; ProgId = "TDxInput.AngleAxis"},
    @{ Clsid = "{740A7479-C7C1-44DA-8A84-B5DE63C78B32}"; ProgId = "TDxInput.Vector3D" }
)

$clsidRoot = "HKLM:\SOFTWARE\Classes\CLSID"

if ($Unregister) {
    Write-Host "Unregistering OpenNDOF TDxInput COM server..." -ForegroundColor Yellow
    foreach ($entry in $clsids) {
        $key = Join-Path $clsidRoot $entry.Clsid
        if (Test-Path $key) {
            Remove-Item $key -Recurse -Force
            Write-Host "  Removed CLSID $($entry.Clsid)"
        }
        $progKey = "HKLM:\SOFTWARE\Classes\$($entry.ProgId)"
        if (Test-Path $progKey) {
            Remove-Item $progKey -Recurse -Force
            Write-Host "  Removed ProgID $($entry.ProgId)"
        }
    }
    Write-Host "Done. Re-run the 3DxWare installer to restore the official driver." -ForegroundColor Green
    return
}

Write-Host "Registering OpenNDOF as TDxInput COM server..." -ForegroundColor Cyan
Write-Host "  comhost : $comhost"
Write-Host "  core    : $coredll"
Write-Host ""

foreach ($entry in $clsids) {
    $clsid = $entry.Clsid
    $progId = $entry.ProgId

    # CLSID\{...}\InprocServer32
    $clsidKey    = Join-Path $clsidRoot $clsid
    $inprocKey   = Join-Path $clsidKey  "InprocServer32"
    $progIdKey   = Join-Path $clsidKey  "ProgID"

    New-Item         -Path $clsidKey  -Force | Out-Null
    Set-ItemProperty -Path $clsidKey  -Name "(Default)" -Value $progId

    New-Item         -Path $inprocKey -Force | Out-Null
    Set-ItemProperty -Path $inprocKey -Name "(Default)"      -Value $comhost
    Set-ItemProperty -Path $inprocKey -Name "ThreadingModel" -Value "Both"
    Set-ItemProperty -Path $inprocKey -Name "RuntimeVersion" -Value "v4.0.30319"

    New-Item         -Path $progIdKey -Force | Out-Null
    Set-ItemProperty -Path $progIdKey -Name "(Default)" -Value $progId

    # ProgID → CLSID back-link
    $progRootKey  = "HKLM:\SOFTWARE\Classes\$progId"
    $progClsidKey = Join-Path $progRootKey "CLSID"
    New-Item         -Path $progRootKey  -Force | Out-Null
    Set-ItemProperty -Path $progRootKey  -Name "(Default)" -Value $progId
    New-Item         -Path $progClsidKey -Force | Out-Null
    Set-ItemProperty -Path $progClsidKey -Name "(Default)" -Value $clsid

    Write-Host "  Registered $progId  $clsid"
}

Write-Host ""
Write-Host "Registration complete." -ForegroundColor Green
Write-Host "OpenNDOF will now receive input from any application that uses the TDxInput COM API."
Write-Host "To undo, run:  .\Register-ComServer.ps1 -Unregister"
