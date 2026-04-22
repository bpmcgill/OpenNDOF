using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenNDOF.Core.Profiles;

/// <summary>Per-application axis scaling and filter settings.</summary>
public sealed class DeviceProfile
{
    public string Name { get; set; } = "default";

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
        catch { /* corrupt file */ }
        EnsureDefault();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_profilePath)!);
        File.WriteAllText(_profilePath,
            JsonSerializer.Serialize(_profiles.Values.ToList(), _json));
    }

    public DeviceProfile Get(string name)
    {
        if (_profiles.TryGetValue(name, out var p)) return p;
        var newProfile = new DeviceProfile { Name = name };
        _profiles[name] = newProfile;
        return newProfile;
    }

    public void AddOrUpdate(DeviceProfile profile) => _profiles[profile.Name] = profile;

    public void Delete(string name) => _profiles.Remove(name);

    private void EnsureDefault()
    {
        if (!_profiles.ContainsKey("default"))
            _profiles["default"] = new DeviceProfile();
    }
}
