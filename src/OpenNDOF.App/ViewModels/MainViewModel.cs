using CommunityToolkit.Mvvm.ComponentModel;

namespace OpenNDOF.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "OpenNDOF Bridge";

    [ObservableProperty]
    private string _subtitle = "Open-source 6-DOF device driver";
}
