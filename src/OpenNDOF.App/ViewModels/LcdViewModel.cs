using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenNDOF.Core.Devices;

namespace OpenNDOF.App.ViewModels;

public sealed partial class LcdViewModel : ObservableObject
{
    private readonly SpaceDevice _device;

    [ObservableProperty] private string _line1         = "OpenNDOF";
    [ObservableProperty] private string _line2         = "SpacePilot";
    [ObservableProperty] private string _line3         = "";
    [ObservableProperty] private string _line4         = "";
    [ObservableProperty] private string _line5         = "";
    [ObservableProperty] private bool   _hasLcd;
    [ObservableProperty] private string _statusMessage = "";

    // Display limits — sourced from SpaceDevice which re-exposes the internal SpacePilotLcd constants
    public int MaxLines => SpaceDevice.LcdMaxLines;
    public int MaxChars => SpaceDevice.LcdCharsPerLine;

    public LcdViewModel(SpaceDevice device)
    {
        _device = device;
        _device.ConnectionChanged += (_, _) =>
            System.Windows.Application.Current.Dispatcher.Invoke(UpdateHasLcd);
        UpdateHasLcd();
    }

    private void UpdateHasLcd()
        => HasLcd = _device.IsConnected && _device.DeviceInfo?.Type == DeviceType.SpacePilot;

    [RelayCommand]
    private void Send()
    {
        if (!HasLcd) { StatusMessage = "No SpacePilot LCD device connected."; return; }
        bool ok = _device.WriteDisplayLines(Line1, Line2, Line3, Line4, Line5);
        StatusMessage = ok ? "Written successfully." : "Write failed — check device connection.";
    }

    [RelayCommand]
    private void Clear()
    {
        if (!HasLcd) { StatusMessage = "No SpacePilot LCD device connected."; return; }
        bool ok = _device.WriteDisplayLines();
        StatusMessage = ok ? "Cleared." : "Clear failed.";
    }
}
