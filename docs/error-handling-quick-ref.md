# Exception Handling Quick Reference

Quick lookup guide for exception handling patterns used in OpenNDOF.

---

## TL;DR

**Every public method should:**
1. ✅ Validate inputs early
2. ✅ Catch specific exception types
3. ✅ Log errors to Debug output
4. ✅ Let UI layer provide user feedback

**Bad pattern:**
```csharp
try { DoSomething(); }
catch { }  // ❌ Silent failure
```

**Good pattern:**
```csharp
try { DoSomething(); }
catch (SpecificException ex)
{
    Debug.WriteLine($"[Component] Error: {ex.Message}");
    throw;  // ← UI layer handles feedback
}
```

---

## By Operation Type

### File I/O (Load/Save)

```csharp
try
{
    File.WriteAllText(path, content);
}
catch (IOException ex)
{
    Debug.WriteLine($"[Component] I/O error: {ex.Message}");
    throw;  // Rethrow for UI to show error
}
catch (UnauthorizedAccessException ex)
{
    Debug.WriteLine($"[Component] Permission denied: {ex.Message}");
    throw;
}
```

### JSON Deserialization

```csharp
try
{
    var data = JsonSerializer.Deserialize<T>(json, options);
}
catch (JsonException ex)
{
    Debug.WriteLine($"[Component] Invalid JSON: {ex.Message}");
    throw;
}
catch (NotSupportedException ex)
{
    Debug.WriteLine($"[Component] Unsupported type: {ex.Message}");
    throw;
}
```

### Input Validation

```csharp
public void Process(string name)
{
    if (string.IsNullOrEmpty(name))
        throw new ArgumentException("Name is required", nameof(name));
    
    if (name.Length > 100)
        throw new ArgumentException("Name too long", nameof(name));
    
    // ... proceed with processing
}
```

### Event Handlers

```csharp
private void OnEvent(object? sender, EventArgs e)
{
    if (profile == null) return;  // ← Defensive check
    if (index < 0 || index >= limit) return;  // ← Bounds check
    
    try
    {
        ProcessEvent();
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[Component] Event processing failed: {ex.Message}");
        // Don't rethrow - event handlers should not crash
    }
}
```

### UI Commands

```csharp
[RelayCommand]
private void SaveData()
{
    try
    {
        PerformSave();
        _snackbar.Show("Success", "Data saved.", 
            ControlAppearance.Success, null, TimeSpan.FromSeconds(3));
    }
    catch (ArgumentException ex)
    {
        _snackbar.Show("Error", $"Invalid data: {ex.Message}", 
            ControlAppearance.Caution, null, TimeSpan.FromSeconds(5));
    }
    catch (IOException ex)
    {
        _snackbar.Show("Error", "Save failed. Check disk space.", 
            ControlAppearance.Caution, null, TimeSpan.FromSeconds(5));
    }
    catch (Exception ex)
    {
        _snackbar.Show("Error", "An unexpected error occurred.", 
            ControlAppearance.Caution, null, TimeSpan.FromSeconds(5));
        Debug.WriteLine($"[SaveData] {ex}");
    }
}
```

### Database/Collection Operations

```csharp
public T Get(string key)
{
    if (string.IsNullOrEmpty(key))
        throw new ArgumentException("Key required", nameof(key));
    
    if (_cache.TryGetValue(key, out var value))
        return value;
    
    // Not found - create new or throw
    throw new KeyNotFoundException($"Key '{key}' not found");
}
```

---

## Debug Logging Format

**Always use:** `[ComponentName]` prefix for context

```csharp
Debug.WriteLine($"[ProfileManager] File not found: {path}");
Debug.WriteLine($"[MacroExecutor] Invalid SendKeys syntax: {ex.Message}");
Debug.WriteLine($"[SaveProfile] Operation completed successfully");
```

**View in VS:** Debug → Windows → Output (then search for `[ComponentName]`)

---

## Exception Types to Catch

| Exception | When | How to Handle |
|-----------|------|---------------|
| `ArgumentException` | Invalid input | Fail fast, show user message |
| `ArgumentNullException` | Null parameter | Validate before use |
| `InvalidOperationException` | Wrong state | Check preconditions |
| `IOException` | Disk/file issues | Log, show disk space message |
| `UnauthorizedAccessException` | Permission denied | Log, show permission message |
| `JsonException` | Bad JSON | Log, recover with defaults |
| `NotSupportedException` | Unsupported operation | Log, show user message |
| `KeyNotFoundException` | Key doesn't exist | Return null or default |
| `OperationCanceledException` | Operation cancelled | Log, user already knows |
| `Exception` | Anything else | Log full details, show generic error |

---

## Common Mistakes

### ❌ Catching Too Broadly
```csharp
try { var data = JsonSerializer.Deserialize<T>(json); }
catch (Exception) { }  // Hides all errors!
```

### ✅ Catch Specific Types
```csharp
try { var data = JsonSerializer.Deserialize<T>(json); }
catch (JsonException ex) 
{
    Debug.WriteLine($"[Component] Bad JSON: {ex.Message}");
    throw;
}
```

---

### ❌ Silent Failures
```csharp
public void Save()
{
    try { File.WriteAllText(path, content); }
    catch { }  // User has no idea it failed!
}
```

### ✅ Log and Propagate
```csharp
public void Save()
{
    try { File.WriteAllText(path, content); }
    catch (IOException ex)
    {
        Debug.WriteLine($"[Component] Save failed: {ex.Message}");
        throw;  // ← UI layer shows message
    }
}
```

---

### ❌ Ignoring Null Checks
```csharp
private void OnButtonPressed(int index)
{
    var action = _profile.ButtonActions[index];  // Crash if _profile is null!
}
```

### ✅ Defensive Checks
```csharp
private void OnButtonPressed(int index)
{
    if (_profile == null) return;
    if (index < 0 || index >= _profile.ButtonActions.Length) return;
    
    var action = _profile.ButtonActions[index];
}
```

---

### ❌ No Input Validation
```csharp
public DeviceProfile Get(string name)
{
    return _profiles[name];  // Crashes if name is empty!
}
```

### ✅ Validate Early
```csharp
public DeviceProfile Get(string name)
{
    if (string.IsNullOrEmpty(name))
        throw new ArgumentException("Name required", nameof(name));
    
    return _profiles[name];
}
```

---

## Checklist for Code Review

- [ ] All public methods have input validation
- [ ] I/O operations are wrapped in try-catch
- [ ] Specific exception types are caught (not bare `catch {}`)
- [ ] Errors are logged to Debug output with `[ComponentName]` prefix
- [ ] UI commands provide snackbar feedback on success/failure
- [ ] Event handlers don't rethrow exceptions
- [ ] Null/defensive checks prevent obvious crashes
- [ ] Tests cover error paths (null input, file not found, etc.)
- [ ] No silent failures (empty catch blocks)
- [ ] User messages are clear and actionable

---

## See Also

- [Full Error Handling Guide](error-handling.md)
- [Architecture Overview](architecture.md)
- [Contributing Guidelines](CONTRIBUTING.md)

---

**Last Updated:** 2026-04-24  
**Test Coverage:** 12 tests covering exception paths  
**Build Status:** ✅ All tests passing
