# Contributing to MAUI Wi-Fi Manager

First off, thank you for considering contributing to MAUI Wi-Fi Manager! It's people like you that make this library better for everyone.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [How Can I Contribute?](#how-can-i-contribute)
- [Development Setup](#development-setup)
- [Coding Guidelines](#coding-guidelines)
- [Submitting Changes](#submitting-changes)
- [Reporting Bugs](#reporting-bugs)
- [Suggesting Enhancements](#suggesting-enhancements)

## Code of Conduct

This project and everyone participating in it is governed by our commitment to providing a welcoming and inspiring community for all. Please be respectful and constructive in your interactions.

## Getting Started

1. Fork the repository
2. Clone your fork: `git clone https://github.com/YOUR_USERNAME/maui_wifi_manager.git`
3. Create a branch: `git checkout -b feature/your-feature-name`
4. Make your changes
5. Push to your fork: `git push origin feature/your-feature-name`
6. Create a Pull Request

## How Can I Contribute?

### Reporting Bugs

Before creating bug reports, please check existing issues to avoid duplicates. When creating a bug report, please include:

- **Use a clear and descriptive title**
- **Describe the exact steps to reproduce the problem**
- **Provide specific examples** (code snippets, screenshots)
- **Describe the behavior you observed** and what you expected
- **Include details about your environment:**
  - OS and version
  - .NET version
  - MAUI version
  - Device/emulator information

Use the bug report template when creating an issue.

### Suggesting Enhancements

Enhancement suggestions are tracked as GitHub issues. When creating an enhancement suggestion, please include:

- **Use a clear and descriptive title**
- **Provide a detailed description** of the suggested enhancement
- **Explain why this enhancement would be useful**
- **Provide examples** of how the feature would be used
- **List any similar features** in other libraries

Use the feature request template when creating an issue.

### Pull Requests

- Fill in the pull request template
- Follow the [Code Style Guide](CODE_STYLE.md)
- Include tests for new features
- Update documentation as needed
- Ensure all tests pass
- Keep pull requests focused on a single concern

## Development Setup

### Prerequisites

- .NET 8.0 or 9.0 SDK
- Visual Studio 2022 (v17.8+) or Visual Studio Code
- MAUI workload installed: `dotnet workload install maui`
- Platform-specific SDKs:
  - **Android**: Android SDK (API 21+)
  - **iOS**: Xcode (requires macOS)
  - **Windows**: Windows SDK 10.0.19041.0+

### Building the Project

```bash
# Clone the repository
git clone https://github.com/exendahal/maui_wifi_manager.git
cd maui_wifi_manager

# Restore dependencies
dotnet restore

# Build the library
cd src/MAUIWifiManager
dotnet build

# Build the sample app
cd ../../samples
dotnet build
```

### Running Tests

```bash
# Run unit tests (when available)
dotnet test

# Run sample app
cd samples
dotnet run
```

## Coding Guidelines

### General Principles

1. **Follow Existing Patterns**: Look at existing code and follow the same style
2. **Write Clean Code**: Code should be self-documenting
3. **Add Tests**: Cover new features and bug fixes with tests
4. **Document Your Code**: Add XML documentation for public APIs
5. **Keep It Simple**: Prefer simple, readable solutions

### Code Style

We use `.editorconfig` to enforce consistent formatting. Please ensure your editor supports EditorConfig.

Key points:
- Use 4 spaces for indentation (no tabs)
- Use PascalCase for public members
- Use camelCase with underscore prefix for private fields (`_fieldName`)
- Add XML documentation to all public APIs
- Use nullable reference types where appropriate

See [CODE_STYLE.md](CODE_STYLE.md) for comprehensive guidelines.

### Platform-Specific Code

When adding platform-specific code:

1. Create separate files with appropriate suffixes:
   - `.android.cs` for Android
   - `.apple.cs` for iOS/macOS
   - `.windows.cs` for Windows
   - `.shared.cs` for shared code

2. Use conditional compilation only when necessary:
   ```csharp
   #if ANDROID
       // Android-specific code
   #endif
   ```

3. Document platform-specific behavior in XML comments

### Commit Messages

Write clear, descriptive commit messages:

```
Add CancellationToken support to async methods

- Updated IWifiNetworkService interface
- Implemented cancellation in all platform implementations
- Added documentation and examples
- Updated tests

Fixes #123
```

Format:
- First line: Brief summary (50 chars or less)
- Blank line
- Detailed description (wrap at 72 chars)
- Reference issues/PRs at the end

### Branch Naming

Use descriptive branch names:
- `feature/add-retry-logic` - New features
- `fix/connection-timeout` - Bug fixes
- `docs/update-readme` - Documentation updates
- `refactor/cleanup-logging` - Code refactoring

## Submitting Changes

### Before Submitting

1. **Test Your Changes**
   - Build successfully on all target platforms
   - Test on actual devices when possible
   - Verify existing tests still pass

2. **Update Documentation**
   - Add/update XML comments
   - Update README.md if needed
   - Update CHANGELOG.md
   - Add examples if introducing new features

3. **Follow Code Style**
   - Run code formatting
   - No compiler warnings
   - Follow naming conventions

### Pull Request Process

1. **Create a Pull Request**
   - Use a clear, descriptive title
   - Fill out the PR template completely
   - Reference related issues

2. **Code Review**
   - Address reviewer feedback
   - Keep discussions constructive
   - Push changes to the same branch

3. **Merge**
   - PRs are merged into the `develop` branch
   - Squash commits if requested
   - Delete your branch after merge

### PR Checklist

- [ ] Code follows style guidelines
- [ ] Self-review completed
- [ ] Comments added for complex code
- [ ] Documentation updated
- [ ] Tests added/updated
- [ ] No new warnings
- [ ] Tested on relevant platforms
- [ ] CHANGELOG.md updated

## Project Structure

```
maui_wifi_manager/
├── src/
│   └── MAUIWifiManager/
│       ├── *.shared.cs           # Shared code
│       ├── *.android.cs          # Android implementation
│       ├── *.apple.cs            # iOS/Mac implementation
│       ├── *.windows.cs          # Windows implementation
│       └── MauiWifiManager.csproj
├── samples/
│   └── DemoApp/                  # Sample application
├── docs/                         # Documentation
├── .github/                      # GitHub workflows
├── README.md
├── CHANGELOG.md
├── CODE_STYLE.md
└── CONTRIBUTING.md
```

## Release Process

Releases are managed by project maintainers:

1. All changes go to `develop` branch
2. When ready for release, `develop` is merged to `main`
3. A tag is created with version number
4. NuGet package is published automatically
5. Release notes are created from CHANGELOG.md

## Need Help?

- Check the [README](README.md) for basic usage
- Read [ADVANCED_USAGE.md](ADVANCED_USAGE.md) for advanced features
- Search [existing issues](https://github.com/exendahal/maui_wifi_manager/issues)
- Ask questions in [Discussions](https://github.com/exendahal/maui_wifi_manager/discussions)

## Recognition

Contributors are recognized in:
- Release notes
- Project README
- Commit history

Thank you for contributing! 🎉
