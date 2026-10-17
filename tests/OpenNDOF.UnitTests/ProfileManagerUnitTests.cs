namespace OpenNDOF.UnitTests;

using OpenNDOF.Core.Profiles;

public class ProfileManagerUnitTests
{
    [Fact]
    public void AddOrUpdate_WithValidProfile_AddsProfile()
    {
        var manager = new ProfileManager();

        manager.AddOrUpdate(new DeviceProfile { Name = "cad" });

        Assert.True(manager.Profiles.ContainsKey("cad"));
        Assert.Equal("cad", manager.Profiles["cad"].Name);
    }

    [Fact]
    public void AddOrUpdate_WithNullProfile_ThrowsArgumentException()
    {
        var manager = new ProfileManager();

        Assert.Throws<ArgumentException>(() => manager.AddOrUpdate(null!));
    }

    [Fact]
    public void AddOrUpdate_WithEmptyName_ThrowsArgumentException()
    {
        var manager = new ProfileManager();

        Assert.Throws<ArgumentException>(() => manager.AddOrUpdate(new DeviceProfile { Name = "" }));
    }

    [Fact]
    public void Get_WithUnknownName_CreatesAndReturnsProfile()
    {
        var manager = new ProfileManager();

        var profile = manager.Get("new-profile");

        Assert.Equal("new-profile", profile.Name);
        Assert.Same(profile, manager.Profiles["new-profile"]);
    }

    [Fact]
    public void Get_WithNullName_ThrowsArgumentException()
    {
        var manager = new ProfileManager();

        Assert.Throws<ArgumentException>(() => manager.Get(null!));
    }

    [Fact]
    public void Delete_RemovesProfile()
    {
        var manager = new ProfileManager();
        manager.AddOrUpdate(new DeviceProfile { Name = "temp" });

        manager.Delete("temp");

        Assert.False(manager.Profiles.ContainsKey("temp"));
    }

    [Fact]
    public void GetByAppName_IsCaseInsensitive()
    {
        var manager = new ProfileManager();
        manager.AddOrUpdate(new DeviceProfile
        {
            Name = "autocad",
            AppNames = ["acad", "autocad"]
        });

        var profile = manager.GetByAppName("ACAD");

        Assert.NotNull(profile);
        Assert.Equal("autocad", profile!.Name);
    }

    [Fact]
    public void GetByAppName_WhenNoMatch_ReturnsNull()
    {
        var manager = new ProfileManager();
        manager.AddOrUpdate(new DeviceProfile { Name = "default" });

        var profile = manager.GetByAppName("unknown-app");

        Assert.Null(profile);
    }

    [Fact]
    public void AddOrUpdate_ReplacesExistingProfile_IgnoringCase()
    {
        var manager = new ProfileManager();
        manager.AddOrUpdate(new DeviceProfile { Name = "cad", ScaleTx = 1.5 });
        manager.AddOrUpdate(new DeviceProfile { Name = "CAD", ScaleTx = 2.5 });

        Assert.Single(manager.Profiles);
        Assert.Equal(2.5, manager.Profiles["cad"].ScaleTx);
    }

    [Fact]
    public void Delete_MissingProfile_DoesNotThrow()
    {
        var manager = new ProfileManager();

        manager.Delete("nope");

        Assert.Empty(manager.Profiles);
    }

    [Fact]
    public void GetByAppName_ReturnsFirstMatchingProfile()
    {
        var manager = new ProfileManager();
        manager.AddOrUpdate(new DeviceProfile { Name = "first", AppNames = ["app"] });
        manager.AddOrUpdate(new DeviceProfile { Name = "second", AppNames = ["app"] });

        Assert.Equal("first", manager.GetByAppName("APP")!.Name);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsViaCustomPath()
    {
        string path = Path.Combine(Path.GetTempPath(), $"opendndof-{Guid.NewGuid():N}.json");
        try
        {
            var writer = new ProfileManager(path);
            writer.AddOrUpdate(new DeviceProfile
            {
                Name = "cad",
                ScaleRx = 0.25,
                DeadzoneRot = 0.1,
                AppNames = ["acad"],
                ButtonLabels = ["Fit", "Pan"]
            });
            writer.Save();

            var reader = new ProfileManager(path);
            reader.Load();

            Assert.True(reader.Profiles.ContainsKey("cad"));
            Assert.Equal(0.25, reader.Profiles["cad"].ScaleRx);
            Assert.Equal("Fit", reader.Profiles["cad"].ButtonLabels[0]);
            Assert.Contains("acad", reader.Profiles["cad"].AppNames);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_WithCorruptJson_ProvidesDefaultProfile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"opendndof-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{ this is not json !!");

            var manager = new ProfileManager(path);
            manager.Load();

            Assert.True(manager.Profiles.ContainsKey("default"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_WithDuplicateNames_ProvidesDefaultProfile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"opendndof-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """[{"Name":"a"},{"Name":"a"}]""");

            var manager = new ProfileManager(path);
            manager.Load();

            Assert.False(manager.Profiles.ContainsKey("a"));
            Assert.True(manager.Profiles.ContainsKey("default"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
