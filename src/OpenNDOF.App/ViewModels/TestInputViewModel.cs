using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenNDOF.Core.Devices;
using OpenNDOF.Core.Input;
using System.Windows;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace OpenNDOF.App.ViewModels;

/// <summary>
/// Drives the 3-D test viewport.  Each sensor update incrementally rotates
/// and translates the displayed cube so the user can visualise all 6 DOF.
/// </summary>
public sealed partial class TestInputViewModel : ObservableObject, IDisposable
{
    private const double RotSensitivity   = 3.0;   // degrees per normalised unit
    private const double TransSensitivity = 0.04;  // world units per normalised unit
    private const double TransClamp       = 2.5;

    private readonly SpaceDevice _device;
    private Quaternion           _accRotation = Quaternion.Identity;
    private double               _accX, _accY, _accZ;

    // ── 3-D transform properties ──────────────────────────────────────────────
    [ObservableProperty] private Quaternion _cubeRotation = Quaternion.Identity;
    [ObservableProperty] private double     _cubePosX;
    [ObservableProperty] private double     _cubePosY;
    [ObservableProperty] private double     _cubePosZ;

    // ── Live axis readout ────────────────────────────────────────────────────
    [ObservableProperty] private double _liveTx;
    [ObservableProperty] private double _liveTy;
    [ObservableProperty] private double _liveTz;
    [ObservableProperty] private double _liveRx;
    [ObservableProperty] private double _liveRy;
    [ObservableProperty] private double _liveRz;

    // Clamped –1…+1 versions used to drive progress-bar widths
    [ObservableProperty] private double _barTx;
    [ObservableProperty] private double _barTy;
    [ObservableProperty] private double _barTz;
    [ObservableProperty] private double _barRx;
    [ObservableProperty] private double _barRy;
    [ObservableProperty] private double _barRz;

    [ObservableProperty] private bool   _isConnected;
    [ObservableProperty] private string _statusText = "Not connected";
    [ObservableProperty] private string _pressedButtons = "—";

    public TestInputViewModel(SpaceDevice device)
    {
        _device = device;
        _device.SensorUpdated     += OnSensorUpdated;
        _device.KeyboardUpdated   += OnKeyboardUpdated;
        _device.ConnectionChanged += OnConnectionChanged;
        RefreshStatus();
    }

    [RelayCommand]
    private void ResetView()
    {
        _accRotation = Quaternion.Identity;
        _accX = _accY = _accZ = 0;
        CubeRotation = Quaternion.Identity;
        CubePosX = CubePosY = CubePosZ = 0;
    }

    private void OnConnectionChanged(object? sender, EventArgs e)
        => Application.Current.Dispatcher.Invoke(RefreshStatus);

    private void RefreshStatus()
    {
        IsConnected = _device.IsConnected;
        StatusText  = _device.IsConnected
            ? $"Connected — {_device.DeviceInfo?.FriendlyName}"
            : "No device connected.  Use the Dashboard to connect.";
    }

    private void OnSensorUpdated(object? sender, SensorState s)
    {
        // Accumulate rotation using quaternion multiplication to avoid gimbal lock
        var deltaQ = new Quaternion(new Vector3D(1, 0, 0), s.Rx * RotSensitivity)
                   * new Quaternion(new Vector3D(0, 1, 0), s.Ry * RotSensitivity)
                   * new Quaternion(new Vector3D(0, 0, 1), s.Rz * RotSensitivity);
        _accRotation = _accRotation * deltaQ;
        _accRotation.Normalize();

        _accX = Math.Clamp(_accX + s.Tx * TransSensitivity, -TransClamp, TransClamp);
        _accY = Math.Clamp(_accY + s.Ty * TransSensitivity, -TransClamp, TransClamp);
        _accZ = Math.Clamp(_accZ + s.Tz * TransSensitivity, -TransClamp, TransClamp);

        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            CubeRotation = _accRotation;
            CubePosX     = _accX;
            CubePosY     = _accY;
            CubePosZ     = _accZ;

            LiveTx = s.Tx; LiveTy = s.Ty; LiveTz = s.Tz;
            LiveRx = s.Rx; LiveRy = s.Ry; LiveRz = s.Rz;
            BarTx  = Math.Clamp(s.Tx, -1, 1);
            BarTy  = Math.Clamp(s.Ty, -1, 1);
            BarTz  = Math.Clamp(s.Tz, -1, 1);
            BarRx  = Math.Clamp(s.Rx, -1, 1);
            BarRy  = Math.Clamp(s.Ry, -1, 1);
            BarRz  = Math.Clamp(s.Rz, -1, 1);
        }, DispatcherPriority.Background);
    }

    private void OnKeyboardUpdated(object? sender, KeyboardState kb)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            PressedButtons = kb.PressedKeys.Any()
                ? string.Join(", ", kb.PressedKeys.Select(k => $"B{k + 1}"))
                : "—";
        }, DispatcherPriority.Background);
    }

    public void Dispose()
    {
        _device.SensorUpdated     -= OnSensorUpdated;
        _device.KeyboardUpdated   -= OnKeyboardUpdated;
        _device.ConnectionChanged -= OnConnectionChanged;
    }
}
