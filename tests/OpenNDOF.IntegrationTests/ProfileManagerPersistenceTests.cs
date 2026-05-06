namespace OpenNDOF.IntegrationTests;

using OpenNDOF.Core.Profiles;

public sealed class ProfileManagerPersistenceTests : IDisposable
{
    private readonly string _appDataPath;
    private readonly string _backupPath;
    private readonly bool _hadExisting;

    public ProfileManagerPersistenceTests()
    {
        _appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenNDOF", "profiles.json");
        _backupPath = _appDataPath + ".bak.test";

        if (File.Exists(_backupPath))
        {
            File.Delete(_backupPath);
        }

        if (File.Exists(_appDataPath))
        {
            _hadExisting = true;
            Directory.CreateDirectory(Path.GetDirectoryName(_backupPath)!);
            File.Copy(_appDataPath, _backupPath, overwrite: true);
        }

        if (File.Exists(_appDataPath))
        {
            File.Delete(_appDataPath);
        }
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsProfilesAndDefault()
    {
        var writer = new ProfileManager();
        writer.AddOrUpdate(new DeviceProfile
        {
            Name = "integration-cad",
            AppNames = ["acad"],
            ScaleTx = 1.5,
            ScaleRy = 0.8,
            ButtonLabels = ["Fit", "Pan", "Zoom", "Orbit", "Top", "Home"]
        });

        writer.Save();

        var reader = new ProfileManager();
        reader.Load();

        Assert.True(reader.Profiles.ContainsKey("default"));
        Assert.True(reader.Profiles.ContainsKey("integration-cad"));

        var loaded = reader.Profiles["integration-cad"];
        Assert.Equal(1.5, loaded.ScaleTx);
        Assert.Equal(0.8, loaded.ScaleRy);
        Assert.Equal("Fit", loaded.ButtonLabels[0]);
        Assert.Contains("acad", loaded.AppNames);
    }

    [Fact]
    public void Load_WithMissingFile_EnsuresDefaultProfile()
    {
        var manager = new ProfileManager();

        manager.Load();

        Assert.True(manager.Profiles.ContainsKey("default"));
    }

    public void Dispose()
    {
        if (File.Exists(_appDataPath))
        {
            File.Delete(_appDataPath);
        }

        if (_hadExisting && File.Exists(_backupPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_appDataPath)!);
            File.Copy(_backupPath, _appDataPath, overwrite: true);
            File.Delete(_backupPath);
        }
    }
}
