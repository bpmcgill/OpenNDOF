using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenNDOF.Core.Profiles;
using System.Collections.ObjectModel;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace OpenNDOF.App.ViewModels;

public sealed partial class ConfigurationViewModel : ObservableObject, IDisposable
{
    private readonly ProfileManager   _profiles;
    private readonly ISnackbarService _snackbar;
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

    // App names (comma-separated in UI)
    [ObservableProperty] private string _appNames = string.Empty;

    // Button labels
    [ObservableProperty] private string _buttonLabel0 = string.Empty;
    [ObservableProperty] private string _buttonLabel1 = string.Empty;
    [ObservableProperty] private string _buttonLabel2 = string.Empty;
    [ObservableProperty] private string _buttonLabel3 = string.Empty;
    [ObservableProperty] private string _buttonLabel4 = string.Empty;
    [ObservableProperty] private string _buttonLabel5 = string.Empty;

    // Button action types
    [ObservableProperty] private MacroType _actionType0 = MacroType.None;
    [ObservableProperty] private MacroType _actionType1 = MacroType.None;
    [ObservableProperty] private MacroType _actionType2 = MacroType.None;
    [ObservableProperty] private MacroType _actionType3 = MacroType.None;
    [ObservableProperty] private MacroType _actionType4 = MacroType.None;
    [ObservableProperty] private MacroType _actionType5 = MacroType.None;

    // Button action keys/text
    [ObservableProperty] private string _actionKeys0 = string.Empty;
    [ObservableProperty] private string _actionKeys1 = string.Empty;
    [ObservableProperty] private string _actionKeys2 = string.Empty;
    [ObservableProperty] private string _actionKeys3 = string.Empty;
    [ObservableProperty] private string _actionKeys4 = string.Empty;
    [ObservableProperty] private string _actionKeys5 = string.Empty;

    /// <summary>All available macro types — bound to the action-type ComboBoxes.</summary>
    public IReadOnlyList<MacroType> MacroTypes { get; } =
        [MacroType.None, MacroType.SendKeys, MacroType.Text];

    public ConfigurationViewModel(ProfileManager profiles, ISnackbarService snackbar)
    {
        _profiles = profiles;
        _snackbar = snackbar;
        _profiles.Load();
        RefreshProfileList();
        LoadSelectedProfile();
    }

    partial void OnSelectedProfileNameChanged(string value) => LoadSelectedProfile();

    [RelayCommand]
    private void SaveProfile()
    {
        try
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
                AppNames      = AppNames
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList(),
                ButtonLabels  = [ButtonLabel0, ButtonLabel1, ButtonLabel2,
                                 ButtonLabel3, ButtonLabel4, ButtonLabel5],
                ButtonActions = [
                    new() { Type = ActionType0, Keys = ActionKeys0 },
                    new() { Type = ActionType1, Keys = ActionKeys1 },
                    new() { Type = ActionType2, Keys = ActionKeys2 },
                    new() { Type = ActionType3, Keys = ActionKeys3 },
                    new() { Type = ActionType4, Keys = ActionKeys4 },
                    new() { Type = ActionType5, Keys = ActionKeys5 },
                ],
            };
            _profiles.AddOrUpdate(profile);
            _profiles.Save();
            RefreshProfileList();
            _snackbar.Show("Saved", $"Profile '{SelectedProfileName}' saved.", ControlAppearance.Success, null, TimeSpan.FromSeconds(3));
        }
        catch (ArgumentException ex)
        {
            _snackbar.Show("Error", $"Invalid profile: {ex.Message}", ControlAppearance.Caution, null, TimeSpan.FromSeconds(5));
            System.Diagnostics.Debug.WriteLine($"[SaveProfile] {ex.Message}");
        }
        catch (Exception ex)
        {
            _snackbar.Show("Error", "Failed to save profile. Check disk space and permissions.", ControlAppearance.Caution, null, TimeSpan.FromSeconds(5));
            System.Diagnostics.Debug.WriteLine($"[SaveProfile] {ex.Message}");
        }
    }

    [RelayCommand]
    private void AddProfile()
    {
        try
        {
            var name = NewProfileName.Trim();
            if (string.IsNullOrEmpty(name)) return;
            _profiles.AddOrUpdate(new DeviceProfile { Name = name });
            _profiles.Save();
            NewProfileName = string.Empty;
            RefreshProfileList();
            SelectedProfileName = name;
            _snackbar.Show("Added", $"Profile '{name}' created.", ControlAppearance.Success, null, TimeSpan.FromSeconds(3));
        }
        catch (ArgumentException ex)
        {
            _snackbar.Show("Error", $"Invalid profile name: {ex.Message}", ControlAppearance.Caution, null, TimeSpan.FromSeconds(5));
            System.Diagnostics.Debug.WriteLine($"[AddProfile] {ex.Message}");
        }
        catch (Exception ex)
        {
            _snackbar.Show("Error", "Failed to create profile. Check disk space and permissions.", ControlAppearance.Caution, null, TimeSpan.FromSeconds(5));
            System.Diagnostics.Debug.WriteLine($"[AddProfile] {ex.Message}");
        }
    }

    [RelayCommand]
    private void DeleteProfile()
    {
        try
        {
            if (SelectedProfileName == "default") return;
            _profiles.Delete(SelectedProfileName);
            _profiles.Save();
            RefreshProfileList();
            SelectedProfileName = "default";
            _snackbar.Show("Deleted", "Profile deleted.", ControlAppearance.Caution, null, TimeSpan.FromSeconds(3));
        }
        catch (Exception ex)
        {
            _snackbar.Show("Error", "Failed to delete profile.", ControlAppearance.Caution, null, TimeSpan.FromSeconds(5));
            System.Diagnostics.Debug.WriteLine($"[DeleteProfile] {ex.Message}");
            RefreshProfileList();
        }
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
        AppNames      = string.Join(", ", p.AppNames);
        ButtonLabel0  = p.ButtonLabels.Length > 0 ? p.ButtonLabels[0] : string.Empty;
        ButtonLabel1  = p.ButtonLabels.Length > 1 ? p.ButtonLabels[1] : string.Empty;
        ButtonLabel2  = p.ButtonLabels.Length > 2 ? p.ButtonLabels[2] : string.Empty;
        ButtonLabel3  = p.ButtonLabels.Length > 3 ? p.ButtonLabels[3] : string.Empty;
        ButtonLabel4  = p.ButtonLabels.Length > 4 ? p.ButtonLabels[4] : string.Empty;
        ButtonLabel5  = p.ButtonLabels.Length > 5 ? p.ButtonLabels[5] : string.Empty;
        LoadAction(p, 0, ref _actionType0, ref _actionKeys0);
        LoadAction(p, 1, ref _actionType1, ref _actionKeys1);
        LoadAction(p, 2, ref _actionType2, ref _actionKeys2);
        LoadAction(p, 3, ref _actionType3, ref _actionKeys3);
        LoadAction(p, 4, ref _actionType4, ref _actionKeys4);
        LoadAction(p, 5, ref _actionType5, ref _actionKeys5);
        // Notify UI — ObservableProperty backing fields are set directly above
        OnPropertyChanged(nameof(ActionType0)); OnPropertyChanged(nameof(ActionKeys0));
        OnPropertyChanged(nameof(ActionType1)); OnPropertyChanged(nameof(ActionKeys1));
        OnPropertyChanged(nameof(ActionType2)); OnPropertyChanged(nameof(ActionKeys2));
        OnPropertyChanged(nameof(ActionType3)); OnPropertyChanged(nameof(ActionKeys3));
        OnPropertyChanged(nameof(ActionType4)); OnPropertyChanged(nameof(ActionKeys4));
        OnPropertyChanged(nameof(ActionType5)); OnPropertyChanged(nameof(ActionKeys5));
    }

    private static void LoadAction(DeviceProfile p, int i,
        ref MacroType type, ref string keys)
    {
        if (i < p.ButtonActions.Length)
        {
            type = p.ButtonActions[i].Type;
            keys = p.ButtonActions[i].Keys;
        }
        else
        {
            type = MacroType.None;
            keys = string.Empty;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
