# OpenNDOF Documentation Index

Complete guide to OpenNDOF documentation, architecture, and development.

---

## 📖 Main Documentation

### [README.md](../README.md)
Main project documentation including:
- Features overview
- Supported devices
- Installation and build instructions
- Getting started guide
- Architecture overview
- COM server registration
- License information

**Start here** if you're new to the project.

---

## 🏗️ Architecture & Design

### [architecture.md](architecture.md)
Detailed internal architecture covering:
- Project structure and dependencies
- Component responsibilities
- Data flow and event propagation
- HID protocol details
- COM API implementation
- Testing strategy

**Read this** to understand how the system works.

---

## 🛡️ Error Handling & Quality

### [error-handling.md](error-handling.md) ⭐ NEW
Comprehensive guide to exception handling in OpenNDOF:
- Exception handling by component
- Best practices and patterns
- Recovery strategies
- Testing approach
- Debugging tips
- Future improvements

**Read this** to understand error handling philosophy and implement robust code.

### [error-handling-quick-ref.md](error-handling-quick-ref.md) ⭐ NEW
Quick reference for exception handling patterns:
- TL;DR summary
- Common patterns by operation type
- Anti-patterns to avoid
- Debug logging format
- Common mistakes
- Code review checklist

**Use this** as a quick lookup while coding.

---

## 👨‍💻 Contributing

### [../CONTRIBUTING.md](../CONTRIBUTING.md) ⭐ NEW
Guidelines for contributing to OpenNDOF:
- Getting started with development
- Code style guidelines
- Exception handling requirements
- Testing expectations
- PR process
- Commit message format
- Development workflow

**Read this** before submitting pull requests.

---

## 🔧 Device Integration

### [adding-a-device.md](adding-a-device.md)
Step-by-step guide to adding support for new devices:
- Finding VID/PID
- Updating KnownDevices
- Verifying report format
- LCD support (if applicable)
- Updating documentation

**Use this** when adding support for new 6-DOF devices.

---

## 📱 LCD Protocol

### [lcd-protocol.md](lcd-protocol.md)
Technical documentation on SpacePilot LCD:
- Protocol specification
- Report types and formats
- Rendering pipeline
- Unicode and emoji support
- Implementation details

**Reference this** when working with LCD display features.

---

## 📚 Quick Navigation

### By Role

**I'm a User:**
- Start with [README.md](../README.md)

**I'm a Developer:**
1. Read [architecture.md](architecture.md)
2. Review [error-handling-quick-ref.md](error-handling-quick-ref.md)
3. Check [../CONTRIBUTING.md](../CONTRIBUTING.md) before submitting code

**I'm Adding a Device:**
- Follow [adding-a-device.md](adding-a-device.md)

**I'm Debugging an Issue:**
- Check [error-handling.md](error-handling.md) for diagnostics
- Use [error-handling-quick-ref.md](error-handling-quick-ref.md) for patterns
- Review relevant source file architecture in [architecture.md](architecture.md)

**I'm Implementing LCD Features:**
- Study [lcd-protocol.md](lcd-protocol.md)
- Review existing `SpacePilotLcd.cs` implementation

---

## 🔑 Key Topics

### Exception Handling
- **Full Guide**: [error-handling.md](error-handling.md)
- **Quick Reference**: [error-handling-quick-ref.md](error-handling-quick-ref.md)
- **Contributing Rules**: [../CONTRIBUTING.md](../CONTRIBUTING.md#exception-handling)

### Architecture
- **System Design**: [architecture.md](architecture.md)
- **Project Structure**: [README.md](../README.md#architecture)

### Quality Assurance
- **Error Handling**: [error-handling.md](error-handling.md#testing-strategy)
- **Testing**: [architecture.md](architecture.md#testing)
- **Contributing**: [../CONTRIBUTING.md](../CONTRIBUTING.md#testing)

### Development
- **Getting Started**: [../CONTRIBUTING.md](../CONTRIBUTING.md#getting-started)
- **Code Style**: [../CONTRIBUTING.md](../CONTRIBUTING.md#code-style)
- **PR Process**: [../CONTRIBUTING.md](../CONTRIBUTING.md#pull-request-process)

---

## 📋 Recent Updates

### 2026-04-24: Exception Handling & Documentation Overhaul
- ✅ Comprehensive exception handling implemented
- ✅ 12 new unit tests added
- ✅ Complete error handling documentation created
- ✅ Contributing guidelines published
- ✅ Quick reference guide added
- ✅ Build: ✅ Passing | Tests: ✅ 12/12 passing

**New Files:**
- `docs/error-handling.md` - Complete exception handling guide
- `docs/error-handling-quick-ref.md` - Quick lookup reference
- `CONTRIBUTING.md` - Contributor guidelines
- `docs/INDEX.md` - This file!

**Modified Files:**
- `README.md` - Added error handling section
- `docs/architecture.md` - Added error handling info
- `ProfileManager.cs` - Improved error handling
- `MacroExecutor.cs` - Added defensive null check
- `ConfigurationViewModel.cs` - Added error handling to all commands
- `UnitTest1.cs` - Added 12 comprehensive tests

---

## 🔗 External References

- [GitHub Repository](https://github.com/bpmcgill/OpenNDOF)
- [Issues Tracker](https://github.com/bpmcgill/OpenNDOF/issues)
- [3DConnexion SDK](https://www.3dconnexion.com/developer)
- [xUnit Testing Framework](https://xunit.net/)
- [WPF-UI Component Library](https://github.com/lepoco/wpfui)

---

## 📞 Getting Help

1. **Search existing issues** on GitHub
2. **Check error-handling.md** for debugging tips
3. **Read architecture.md** to understand the system
4. **Open a new issue** with:
   - Clear description
   - Steps to reproduce
   - Error messages and logs (from Debug output)
   - System information

---

## ✅ Documentation Checklist

- [x] Main README with features and quick start
- [x] Architecture documentation
- [x] Error handling guide (comprehensive)
- [x] Error handling quick reference
- [x] Contributing guidelines
- [x] Device addition guide
- [x] LCD protocol documentation
- [x] Documentation index (this file)
- [x] Exception handling in code (implementation complete)
- [x] Unit tests covering error paths
- [x] Build verification (✅ passing)

---

**Last Updated**: 2026-04-24  
**Status**: ✅ Complete and verified  
**Test Coverage**: 12/12 tests passing  
**Documentation**: Comprehensive

For questions or contributions, please refer to [../CONTRIBUTING.md](../CONTRIBUTING.md).
