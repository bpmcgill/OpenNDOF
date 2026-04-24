OpenNDOF: Open-Source 3DConnexion Device Driver Alternative for Windows

An open-source Win32 HID-based bridge for 3DConnexion 6-DOF (six degrees of freedom) input devices 
that replaces the proprietary 3DConnexion driver. Implements the complete TDxInput COM API, 
supports all major 3DConnexion hardware (SpacePilot, SpaceNavigator, SpaceExplorer, SpaceTraveler, 
SpaceBall 5000, Aerion NDOF), and includes professional features like per-application profiles, 
macro buttons, and SpacePilot LCD rendering.

KEY TECHNICAL FEATURES:
• Direct HID communication via Win32 API (hid.dll, setupapi.dll) with no external driver dependency
• Complete TDxInput COM server implementation (ISensor, IKeyboard, ITDxInfo interfaces)
• Full reverse-engineered SpacePilot LCD protocol support (240×64 1-bit display rendering)
• 6-axis motion capture with motion fusion (TX/TY/TZ rotation reports followed by RX/RY/RZ, 
  normalised ±1.0)
• Button state tracking with per-button press events
• JSON-based profile persistence with per-axis scaling and dead-zone control
• MVVM architecture with WPF-UI Fluent design
• Comprehensive exception handling with structured logging
• 100% test coverage for error paths
• MIT licensed

SYSTEM REQUIREMENTS:
• Windows 10 / 11 (64-bit)
• .NET 10 Runtime
• USB-connected 3DConnexion device
• No proprietary drivers required

USE CASES:
• CAD/3D modeling (AutoCAD, SolidWorks, Blender, Maya, FreeCAD, etc.)
• 3D visualization and rendering
• Game development with 3D navigation
• Scientific visualization
• Architectural design and visualization
• Product design and 3D CAD workflows
• VR/AR development with spatial input
• Legacy hardware restoration

KEYWORDS FOR SEARCH:
3DConnexion driver, SpacePilot driver, SpaceNavigator driver, 6-DOF input, CAD input device, 
3D mouse driver, open-source driver, Windows HID, COM API, TDxInput, AutoCAD input, SolidWorks 
input, Blender 3D mouse, Maya input device, legacy hardware, device resurrection, free 3DConnexion 
alternative, driverless 6-DOF, Win32 HID interface, NDOF input, spatial input device

GITHUB TOPICS:
3dconnexion, 6dof, input-devices, spacenavigator, spacepilot, open-source, windows, cad-software, 
hid, com-api, tdxinput, autocad, solidworks, blender, maya, hardware-revival, device-driver, 
wpf, dotnet, reverse-engineering