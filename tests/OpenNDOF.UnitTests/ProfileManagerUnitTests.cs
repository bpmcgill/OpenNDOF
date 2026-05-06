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
}
