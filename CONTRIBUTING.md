# Contributing to OpenNDOF

Thank you for your interest in contributing to OpenNDOF! This guide will help you understand the project structure and coding standards.

---

## Getting Started

1. **Fork and clone the repository**
   ```bash
   git clone https://github.com/YOUR_USERNAME/OpenNDOF.git
   cd OpenNDOF
   ```

2. **Build the solution**
   ```bash
   dotnet build
   ```

3. **Run the dashboard**
   ```bash
   dotnet run --project src/OpenNDOF.App
   ```

4. **Run tests**
   ```bash
   dotnet test
   ```

---

## Code Style

### General Guidelines

- Follow C# naming conventions: `PascalCase` for public members, `_camelCase` for private fields
- Use meaningful variable and method names
- Keep methods focused and reasonably sized (aim for < 30 lines)
- Use `readonly` for fields that are not reassigned
- Prefer composition over inheritance

### Naming Conventions

```csharp
// ✅ Good
public class ProfileManager { }
private string _profilePath;
public event EventHandler<SensorState>? SensorUpdated;
private void OnReportReceived(HidReport report) { }

// ❌ Bad
public class ProfileMgr { }
private string profilePath;
public event EventHandler<SensorState>? sensorUpdated;
private void OnReportReceived_Internal(HidReport report) { }
```

### Formatting

- 4-space indentation (VS default)
- Braces on new lines (Allman style)
- Space before opening brace in method declarations
- Use `var` for obvious types, explicit types for clarity

```csharp
// ✅ Good
public void Connect(string profileName = "default")
{
    var hid = HidController.Instance;
    if (hid == null)
    {
        return;
    }

    List<SupportedDevice> devices = _hid.GetConnectedDevices();
}

// ❌ Bad
public void Connect(string profileName="default"){var hid=HidController.Instance;
if(hid==null)return;
List<SupportedDevice>devices=_hid.GetConnectedDevices();}
```

---

## Exception Handling

**All changes involving I/O, user input, or external resources must include proper exception handling.**

### Required Patterns

#### 1. **Input Validation**
```csharp
public DeviceProfile Get(string name)
{
    if (string.IsNullOrEmpty(name))
        throw new ArgumentException("Profile name cannot be null or empty", nameof(name));
    
    // ... implementation
}
```

#### 2. **I/O Operation Protection**
```csharp
public void Save()
{
    try
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, content);
    }
    catch (IOException ex)
    {
        System.Diagnostics.Debug.WriteLine($"[ComponentName] I/O error: {ex.Message}");
        throw;  // Let caller (UI layer) handle user feedback
    }
}
```

#### 3. **Event Handler Safety**
```csharp
private void OnButtonPressed(object? sender, int buttonIndex)
{
    if (profile == null) return;  // Defensive null check
    if (buttonIndex < 0 || buttonIndex >= profile.ButtonActions.Length) return;
    
    // ... implementation
}
```

#### 4. **User-Facing Operations**
```csharp
[RelayCommand]
private void SaveProfile()
{
    try
    {
        _profiles.Save();
        _snackbar.Show("Saved", "Profile saved.", ControlAppearance.Success, null, TimeSpan.FromSeconds(3));
    }
    catch (ArgumentException ex)
    {
        _snackbar.Show("Error", $"Invalid profile: {ex.Message}", ControlAppearance.Caution, null, TimeSpan.FromSeconds(5));
    }
    catch (Exception ex)
    {
        _snackbar.Show("Error", "Failed to save profile.", ControlAppearance.Caution, null, TimeSpan.FromSeconds(5));
        System.Diagnostics.Debug.WriteLine($"[SaveProfile] {ex}");
    }
}
```

### Anti-Patterns ❌

```csharp
// ❌ Don't: Swallow exceptions silently
catch { }

// ❌ Don't: Assume non-null
var x = _dict[key];  // Crashes if key doesn't exist

// ❌ Don't: Broad exception catching without logging
catch (Exception) { }

// ❌ Don't: Store exceptions without context
try { /* ... */ } catch (Exception ex) { throw; }
```

See [Error Handling Guide](docs/error-handling.md) for comprehensive documentation.

---

## Testing

### Requirements

- **All new code paths must have unit tests**
- **Edge cases and error paths must be tested**
- Tests should be in `OpenNDOF.Tests`
- Use **xUnit** for test framework

### Writing Tests

```csharp
public class ProfileManagerTests
{
    [Fact]
    public void Get_WithNullName_ThrowsArgumentException()
    {
        var manager = new ProfileManager();
        
        Assert.Throws<ArgumentException>(() => manager.Get(null!));
    }

    [Fact]
    public void Save_WithValidProfile_Succeeds()
    {
        var manager = new ProfileManager();
        manager.AddOrUpdate(new DeviceProfile { Name = "test" });
        
        manager.Save();  // Should not throw
    }

    [Fact]
    public void Load_WithMissingFile_EnsuresDefault()
    {
        var manager = new ProfileManager();
        
        manager.Load();
        
        Assert.Contains("default", manager.Profiles.Keys);
    }
}
```

### Running Tests

```bash
dotnet test
dotnet test --verbosity detailed
dotnet test -- --filter "ProfileManager"
```

---

## Documentation

### Code Comments

Use comments sparingly. Code should be self-documenting through clear naming.

```csharp
// ✅ Good: Only for non-obvious logic
// Escape special SendKeys characters so the string is sent literally
// Special characters: + ^ % ~ ( ) { } [ ]
string escaped = EscapeSendKeys(action.Keys);

// ❌ Bad: Obvious comments add noise
// Loop through profiles
foreach (var profile in profiles)
{
    // Check if name is not empty
    if (!string.IsNullOrEmpty(profile.Name))
    {
        // Add to list
        list.Add(profile);
    }
}
```

### XML Documentation

Public APIs should have XML documentation:

```csharp
/// <summary>
/// Loads profiles from disk. Silently recovers from corruption by ensuring
/// the "default" profile is always available.
/// </summary>
public void Load()
{
    // ...
}

/// <summary>
/// Gets or creates a profile by name.
/// </summary>
/// <param name="name">The profile name. Cannot be null or empty.</param>
/// <returns>The profile, creating it if it doesn't exist.</returns>
/// <exception cref="ArgumentException">Thrown if name is null or empty.</exception>
public DeviceProfile Get(string name)
{
    // ...
}
```

---

## Pull Request Process

1. **Create a feature branch**
   ```bash
   git checkout -b feature/my-feature
   git checkout -b fix/issue-123
   ```

2. **Keep commits focused**
   - One feature or fix per commit
   - Write clear commit messages: "Add error handling to SaveProfile" not "WIP"

3. **Test locally**
   ```bash
   dotnet build
   dotnet test
   ```

4. **Submit a PR**
   - Link related issues
   - Describe what changed and why
   - Include before/after screenshots if UI changes
   - Ensure all tests pass

5. **Respond to reviews**
   - Address feedback promptly
   - Push new commits (don't rebase while under review)

---

## Commit Message Format

Follow conventional commit format:

```
type(scope): subject

body

footer
```

Examples:
```
feat(profiles): add error handling to SaveProfile

Added try-catch blocks and snackbar notifications for user feedback.

Fixes #123
```

```
fix(macro): null profile on button press

Added defensive null check to prevent NullReferenceException.

Closes #456
```

Types:
- `feat` — new feature
- `fix` — bug fix
- `docs` — documentation changes
- `test` — test additions/changes
- `refactor` — code refactoring without behavior change
- `perf` — performance improvement
- `ci` — CI/CD changes

---

## Project Structure

```
OpenNDOF/
├── src/
│   ├── OpenNDOF.HID/         — Win32 HID wrapper
│   ├── OpenNDOF.Core/        — Device logic, profiles, COM API
│   └── OpenNDOF.App/         — WPF dashboard
├── tests/
│   └── OpenNDOF.Tests/       — xUnit tests
├── docs/
│   ├── architecture.md       — Internal structure
│   ├── error-handling.md     — Exception strategies
│   └── lcd-protocol.md       — SpacePilot LCD docs
└── README.md                 — Main documentation
```

---

## Development Workflow

### Adding a New Feature

1. **Create a branch**: `git checkout -b feature/my-feature`
2. **Write tests first** (TDD recommended)
3. **Implement the feature**
4. **Add/update documentation**
5. **Run full test suite**: `dotnet test`
6. **Submit PR**

### Fixing a Bug

1. **Create a branch**: `git checkout -b fix/issue-123`
2. **Write a failing test** that reproduces the bug
3. **Fix the bug** (test should now pass)
4. **Verify no regressions**: `dotnet test`
5. **Submit PR with issue link**

### Debugging

- Use Visual Studio debugger or `dotnet debug`
- Check Debug output for `[ComponentName]` prefixed logs
- Use `System.Diagnostics.Debug.WriteLine()` for temporary diagnostics
- Remove temporary debug code before submitting PR

---

## Architecture Decisions

### Adding a New Component

If adding a new public API:

1. **Place it in appropriate namespace** (`OpenNDOF.Core.Devices`, `OpenNDOF.Core.Profiles`, etc.)
2. **Add comprehensive error handling** (see Error Handling section)
3. **Write unit tests** for all public methods
4. **Add XML documentation**
5. **Update architecture.md** if structural changes
6. **Add usage examples** to README if user-facing

### Modifying Existing APIs

- Maintain backward compatibility when possible
- Use deprecation attributes if breaking changes necessary
- Add unit tests for new behavior
- Update relevant documentation

---

## Code Review Checklist

Before submitting, ensure:

- [ ] Code builds without errors
- [ ] All tests pass (`dotnet test`)
- [ ] No compiler warnings (fix pre-existing ones if touching that code)
- [ ] Exception handling present for I/O and user input
- [ ] Public APIs have XML documentation
- [ ] Commit messages are clear and focused
- [ ] No `TODO` or `HACK` comments left behind
- [ ] No debug/temporary code included

Reviewers will check:

- [ ] Logic correctness and edge cases
- [ ] Exception handling is appropriate
- [ ] Tests provide sufficient coverage
- [ ] Code style consistency
- [ ] Documentation accuracy
- [ ] Performance impact (if applicable)

---

## Resources

- [C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [xUnit Documentation](https://xunit.net/)
- [Error Handling Guide](docs/error-handling.md)
- [Architecture Overview](docs/architecture.md)
- [OpenNDOF GitHub Issues](https://github.com/bpmcgill/OpenNDOF/issues)

---

## Questions?

Feel free to:
- Open an issue for questions or discussions
- Comment on existing PRs
- Reach out to maintainers

Thank you for contributing! 🙏
