using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenNDOF.Core.Devices;
using OpenNDOF.Core.Profiles;
using System.Collections.ObjectModel;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace OpenNDOF.App.ViewModels;

public sealed partial class ConfigurationViewModel : ObservableObject, IDisposable
{
    private readonly ProfileManager   _profiles;
    private readonly ISnackbarService _snackbar;
    private readonly SpaceDevice      _device;
    private          bool             _disposed;

    [ObservableProperty] private ObservableCollection<string> _profileNames = [];
    [ObservableProperty] private string  _selectedProfileName = "default";
    [ObservableProperty] private string  _newProfileName      = string.Empty;

    // Translation scale
    [ObservableProperty] private double _scaleTx = 1.0;
    [ObservableProperty] private double _scaleTy = 1.0;
    [ObservableProperty] private double _scaleTz = 1.0;
    [ObservableProperty] private double _deadzoneTrans = 0.0;

    // Rotation scale
    [ObservableProperty] private double _scaleRx = 1.0;
    [ObservableProperty] private double _scaleRy = 1.0;
    [ObservableProperty] private double _scaleRz = 1.0;
    [ObservableProperty] private double _deadzoneRot = 0.0;

    public ConfigurationViewModel(ProfileManager profiles, ISnackbarService snackbar, SpaceDevice device)
    {
        _profiles = profiles;
        _snackbar = snackbar;
        _device   = device;
        _profiles.Load();
        RefreshProfileList();
        LoadSelectedProfile();
    }

    partial void OnSelectedProfileNameChanged(string value) => LoadSelectedProfile();

    [RelayCommand]
    private void SaveProfile()
    {
        var profile = new DeviceProfile
        {
            Name          = SelectedProfileName,
            ScaleTx       = ScaleTx,
            ScaleTy       = ScaleTy,
            ScaleTz       = ScaleTz,
            DeadzoneTrans = DeadzoneTrans,
            ScaleRx       = ScaleRx,
            ScaleRy       = ScaleRy,
            ScaleRz       = ScaleRz,
            DeadzoneRot   = DeadzoneRot,
        };
        _profiles.AddOrUpdate(profile);
        _profiles.Save();
        RefreshProfileList();
        _snackbar.Show("Saved", $"Profile '{SelectedProfileName}' saved.", ControlAppearance.Success, null, TimeSpan.FromSeconds(3));
    }

    [RelayCommand]
    private void AddProfile()
    {
        var name = NewProfileName.Trim();
        if (string.IsNullOrEmpty(name)) return;
        _profiles.AddOrUpdate(new DeviceProfile { Name = name });
        _profiles.Save();
        NewProfileName = string.Empty;
        RefreshProfileList();
        SelectedProfileName = name;
    }

    [RelayCommand]
    private void DeleteProfile()
    {
        if (SelectedProfileName == "default") return;
        _profiles.Delete(SelectedProfileName);
        _profiles.Save();
        RefreshProfileList();
        SelectedProfileName = "default";
        _snackbar.Show("Deleted", "Profile deleted.", ControlAppearance.Caution, null, TimeSpan.FromSeconds(3));
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        ScaleTx = ScaleTy = ScaleTz = 1.0;
        ScaleRx = ScaleRy = ScaleRz = 1.0;
        DeadzoneTrans = DeadzoneRot = 0.0;
    }

    private void RefreshProfileList()
    {
        ProfileNames.Clear();
        foreach (var name in _profiles.Profiles.Keys.OrderBy(n => n))
            ProfileNames.Add(name);

        if (!ProfileNames.Contains(SelectedProfileName))
            SelectedProfileName = ProfileNames.FirstOrDefault() ?? "default";
    }

    private void LoadSelectedProfile()
    {
        var p = _profiles.Get(SelectedProfileName);
        ScaleTx       = p.ScaleTx;
        ScaleTy       = p.ScaleTy;
        ScaleTz       = p.ScaleTz;
        DeadzoneTrans = p.DeadzoneTrans;
        ScaleRx       = p.ScaleRx;
        ScaleRy       = p.ScaleRy;
        ScaleRz       = p.ScaleRz;
        DeadzoneRot   = p.DeadzoneRot;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
