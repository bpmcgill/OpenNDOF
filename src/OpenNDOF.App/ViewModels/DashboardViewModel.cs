using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenNDOF.Core.Devices;
using OpenNDOF.Core.Input;
using System.Windows;

namespace OpenNDOF.App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly SpaceDevice _device;

    [ObservableProperty] private bool   _isConnected;
    [ObservableProperty] private string _deviceName    = "No device detected";
    [ObservableProperty] private string _deviceType    = "—";
    [ObservableProperty] private string _activeProfile = "default";
    [ObservableProperty] private string _statusMessage = "Searching for device…";

    // Live axis values (–1 … +1)
    [ObservableProperty] private double _tx;
    [ObservableProperty] private double _ty;
    [ObservableProperty] private double _tz;
    [ObservableProperty] private double _rx;
    [ObservableProperty] private double _ry;
    [ObservableProperty] private double _rz;

    // Raw display strings
    [ObservableProperty] private string _txLabel = "0.00";
    [ObservableProperty] private string _tyLabel = "0.00";
    [ObservableProperty] private string _tzLabel = "0.00";
    [ObservableProperty] private string _rxLabel = "0.00";
    [ObservableProperty] private string _ryLabel = "0.00";
    [ObservableProperty] private string _rzLabel = "0.00";

    // Button states as comma-separated key codes
    [ObservableProperty] private string _pressedButtons = "—";

    public DashboardViewModel(SpaceDevice device)
    {
        _device = device;
        _device.ConnectionChanged += OnConnectionChanged;
        _device.SensorUpdated     += OnSensorUpdated;
        _device.KeyboardUpdated   += OnKeyboardUpdated;
        RefreshConnectionStatus();
    }

    [RelayCommand]
    private void Connect()
    {
        StatusMessage = "Connecting…";
        bool ok = _device.Connect(ActiveProfile);
        StatusMessage = ok ? "Connected." : "No supported device found. Check USB connection.";
    }

    [RelayCommand]
    private void Disconnect() => _device.Disconnect();

    private void OnConnectionChanged(object? sender, EventArgs e)
        => Application.Current.Dispatcher.Invoke(RefreshConnectionStatus);

    private void RefreshConnectionStatus()
    {
        IsConnected   = _device.IsConnected;
        DeviceName    = _device.DeviceInfo?.FriendlyName ?? "No device detected";
        DeviceType    = _device.DeviceInfo?.Type.ToString() ?? "—";
        StatusMessage = _device.IsConnected
            ? $"Connected to {DeviceName}"
            : "Not connected – click Connect to scan.";
    }

    private void OnSensorUpdated(object? sender, SensorState s)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Tx = Math.Clamp(s.Tx, -1, 1);
            Ty = Math.Clamp(s.Ty, -1, 1);
            Tz = Math.Clamp(s.Tz, -1, 1);
            Rx = Math.Clamp(s.Rx, -1, 1);
            Ry = Math.Clamp(s.Ry, -1, 1);
            Rz = Math.Clamp(s.Rz, -1, 1);
            TxLabel = s.Tx.ToString("F3");
            TyLabel = s.Ty.ToString("F3");
            TzLabel = s.Tz.ToString("F3");
            RxLabel = s.Rx.ToString("F3");
            RyLabel = s.Ry.ToString("F3");
            RzLabel = s.Rz.ToString("F3");
        });
    }

    private void OnKeyboardUpdated(object? sender, KeyboardState kb)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            PressedButtons = kb.PressedKeys.Any()
                ? string.Join(", ", kb.PressedKeys)
                : "—";
        });
    }

    public void Dispose()
    {
        _device.ConnectionChanged -= OnConnectionChanged;
        _device.SensorUpdated     -= OnSensorUpdated;
        _device.KeyboardUpdated   -= OnKeyboardUpdated;
    }
}
