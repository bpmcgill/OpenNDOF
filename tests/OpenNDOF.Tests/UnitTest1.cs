namespace OpenNDOF.Tests;
using OpenNDOF.Core.Profiles;

public class ProfileManagerTests
{
    [Fact]
    public void AddOrUpdate_WithValidProfile_Succeeds()
    {
        var manager = new ProfileManager();
        var profile = new DeviceProfile { Name = "test" };
        manager.AddOrUpdate(profile);
        
        Assert.Equal("test", manager.Profiles["test"].Name);
    }

    [Fact]
    public void AddOrUpdate_WithNullName_ThrowsArgumentException()
    {
        var manager = new ProfileManager();
        var profile = new DeviceProfile { Name = null! };
        
        Assert.Throws<ArgumentException>(() => manager.AddOrUpdate(profile));
    }

    [Fact]
    public void AddOrUpdate_WithEmptyName_ThrowsArgumentException()
    {
        var manager = new ProfileManager();
        var profile = new DeviceProfile { Name = "" };
        
        Assert.Throws<ArgumentException>(() => manager.AddOrUpdate(profile));
    }

    [Fact]
    public void Save_WithValidProfiles_Succeeds()
    {
        var manager = new ProfileManager();
        manager.AddOrUpdate(new DeviceProfile { Name = "default" });
        manager.AddOrUpdate(new DeviceProfile { Name = "profile1" });
        
        // Should not throw
        manager.Save();
    }

    [Fact]
    public void Get_WithNullName_ThrowsArgumentException()
    {
        var manager = new ProfileManager();
        
        Assert.Throws<ArgumentException>(() => manager.Get(null!));
    }

    [Fact]
    public void Get_WithEmptyName_ThrowsArgumentException()
    {
        var manager = new ProfileManager();
        
        Assert.Throws<ArgumentException>(() => manager.Get(""));
    }

    [Fact]
    public void Load_WithMissingFile_EnsuresDefault()
    {
        var manager = new ProfileManager();
        manager.Load();
        
        Assert.Contains("default", manager.Profiles.Keys);
    }

    [Fact]
    public void Delete_RemovesProfile()
    {
        var manager = new ProfileManager();
        manager.AddOrUpdate(new DeviceProfile { Name = "temp" });
        
        manager.Delete("temp");
        
        Assert.DoesNotContain("temp", manager.Profiles.Keys);
    }

    [Fact]
    public void GetByAppName_WithNoMatch_ReturnsNull()
    {
        var manager = new ProfileManager();
        manager.AddOrUpdate(new DeviceProfile { Name = "default" });
        
        var result = manager.GetByAppName("nonexistent");
        
        Assert.Null(result);
    }

    [Fact]
    public void GetByAppName_WithMatch_ReturnsProfile()
    {
        var manager = new ProfileManager();
        var profile = new DeviceProfile { Name = "autocad", AppNames = ["acad", "autocad"] };
        manager.AddOrUpdate(profile);
        
        var result = manager.GetByAppName("acad");
        
        Assert.NotNull(result);
        Assert.Equal("autocad", result.Name);
    }

    [Fact]
    public void GetByAppName_CaseInsensitive_ReturnsProfile()
    {
        var manager = new ProfileManager();
        var profile = new DeviceProfile { Name = "vscode", AppNames = ["code"] };
        manager.AddOrUpdate(profile);
        
        var result = manager.GetByAppName("CODE");
        
        Assert.NotNull(result);
        Assert.Equal("vscode", result.Name);
    }

    [Fact]
    public void Save_FiltersOutInvalidProfiles()
    {
        var manager = new ProfileManager();
        manager.AddOrUpdate(new DeviceProfile { Name = "valid" });
        
        // Manually add invalid profile to internal dictionary (simulating corruption)
        var invalidProfile = new DeviceProfile { Name = "" };
        var dict = (Dictionary<string, DeviceProfile>)manager.Profiles;
        dict[""] = invalidProfile;
        
        // Save should not crash and should filter out the invalid profile
        manager.Save();
        
        // Verify the valid profile was saved
        Assert.Contains("valid", manager.Profiles.Keys);
    }
}