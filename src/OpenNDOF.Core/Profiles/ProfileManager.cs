using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenNDOF.Core.Profiles;

/// <summary>What a macro button does when pressed.</summary>
public enum MacroType
{
    /// <summary>No action — button is label-only.</summary>
    None,
    /// <summary>
    /// Simulate a keyboard shortcut using <see cref="System.Windows.Forms.SendKeys"/> syntax.
    /// Examples: <c>^z</c> = Ctrl+Z, <c>^{F4}</c> = Ctrl+F4, <c>%{F4}</c> = Alt+F4, <c>{F5}</c> = F5.
    /// </summary>
    SendKeys,
    /// <summary>Type literal text into the focused application.</summary>
    Text,
}

/// <summary>Action bound to a single macro button.</summary>
public sealed class ButtonAction
{
    public MacroType Type { get; set; } = MacroType.None;
    /// <summary>
    /// For <see cref="MacroType.SendKeys"/>: SendKeys string (e.g. <c>^z</c>).
    /// For <see cref="MacroType.Text"/>: literal text to type.
    /// </summary>
    public string Keys { get; set; } = "";
}

/// <summary>Per-application axis scaling and filter settings.</summary>
public sealed class DeviceProfile
{
    public string Name { get; set; } = "default";

    /// <summary>
    /// Executable names (without path or extension, case-insensitive) that
    /// trigger this profile when they become the foreground window.
    /// E.g. ["autocad", "acad"]
    /// </summary>
    public List<string> AppNames { get; set; } = [];

    // Translation
    public double ScaleTx  { get; set; } = 1.0;
    public double ScaleTy  { get; set; } = 1.0;
    public double ScaleTz  { get; set; } = 1.0;
    public double DeadzoneTrans { get; set; } = 0.0;

    // Rotation
    public double ScaleRx  { get; set; } = 1.0;
    public double ScaleRy  { get; set; } = 1.0;
    public double ScaleRz  { get; set; } = 1.0;
    public double DeadzoneRot { get; set; } = 0.0;

    /// <summary>
    /// Labels for the 6 macro buttons (indices 0-5).
    /// Shown on the SpacePilot LCD in a 2-column × 3-row grid.
    /// </summary>
    public string[] ButtonLabels { get; set; } = ["", "", "", "", "", ""];

    /// <summary>
    /// Actions executed when each macro button is pressed (indices 0-5).
    /// </summary>
    public ButtonAction[] ButtonActions { get; set; } =
        [new(), new(), new(), new(), new(), new()];
}

/// <summary>
/// Loads and saves named <see cref="DeviceProfile"/> entries to a
/// JSON file in %APPDATA%\OpenNDOF\profiles.json.
/// </summary>
public sealed class ProfileManager
{
    private static readonly string _profilePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "OpenNDOF", "profiles.json");

    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented            = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition   = JsonIgnoreCondition.WhenWritingNull
    };

    private Dictionary<string, DeviceProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, DeviceProfile> Profiles => _profiles;

    public void Load()
    {
        try
        {
            if (!File.Exists(_profilePath)) { EnsureDefault(); return; }
            var list = JsonSerializer.Deserialize<List<DeviceProfile>>(
                           File.ReadAllText(_profilePath), _json) ?? [];
            _profiles = list.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        }
        catch (System.Text.Json.JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileManager] Corrupt JSON file: {ex.Message}");
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileManager] I/O error reading profiles: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileManager] Duplicate profile names in file: {ex.Message}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileManager] Unexpected error loading profiles: {ex.Message}");
        }
        EnsureDefault();
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_profilePath);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }

            var validProfiles = _profiles.Values
                .Where(p => !string.IsNullOrEmpty(p.Name))
                .ToList();

            File.WriteAllText(_profilePath,
                JsonSerializer.Serialize(validProfiles, _json));
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileManager] I/O error saving profiles: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileManager] Error saving profiles: {ex.Message}");
            throw;
        }
    }

    public DeviceProfile Get(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Profile name cannot be null or empty", nameof(name));
        }

        if (_profiles.TryGetValue(name, out var p)) return p;
        var newProfile = new DeviceProfile { Name = name };
        _profiles[name] = newProfile;
        return newProfile;
    }

    public void AddOrUpdate(DeviceProfile profile)
    {
        if (string.IsNullOrEmpty(profile?.Name)) throw new ArgumentException("Profile name cannot be null or empty", nameof(profile));
        _profiles[profile.Name] = profile;
    }

    public void Delete(string name) => _profiles.Remove(name);

    /// <summary>
    /// Returns the first profile whose <see cref="DeviceProfile.AppNames"/> list
    /// contains <paramref name="processName"/> (case-insensitive), or
    /// <c>null</c> if no profile matches.
    /// </summary>
    public DeviceProfile? GetByAppName(string processName) =>
        _profiles.Values.FirstOrDefault(p =>
            p.AppNames.Any(n => n.Equals(processName, StringComparison.OrdinalIgnoreCase)));

    private void EnsureDefault()
    {
        if (!_profiles.ContainsKey("default"))
            _profiles["default"] = new DeviceProfile();
    }
}
