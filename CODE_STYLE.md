# Code Style Guide

This document outlines the coding standards and best practices for the MAUI Wi-Fi Manager project.

## General Principles

1. **Readability First**: Code is read more often than it's written. Prioritize clarity over cleverness.
2. **Consistency**: Follow existing patterns in the codebase.
3. **Documentation**: Public APIs must have XML documentation.
4. **Testing**: Write tests for new features and bug fixes.
5. **SOLID Principles**: Follow SOLID design principles where applicable.

## Code Formatting

We use `.editorconfig` to enforce consistent formatting. Key rules:

- **Indentation**: 4 spaces (no tabs)
- **Line Endings**: CRLF (Windows style)
- **Encoding**: UTF-8
- **Trailing Whitespace**: Remove from all lines
- **Final Newline**: Always include

## Naming Conventions

### General Rules

- **PascalCase** for: classes, methods, properties, events, enums, namespaces
- **camelCase** for: local variables, method parameters
- **_camelCase** for: private fields (with underscore prefix)
- **UPPERCASE** for: constants

### Examples

```csharp
// Classes and Interfaces
public class WifiNetworkService { }
public interface IWifiNetworkService { }

// Methods
public async Task<bool> ConnectWifi(string ssid, string password) { }

// Properties
public int ConnectionTimeoutSeconds { get; set; }

// Private fields
private static Context _Context;

// Local variables and parameters
string currentSsid = "MyNetwork";

// Constants
private const int MAX_RETRY_COUNT = 5;
```

## C# Language Features

### Use Modern C# Features

```csharp
// ✅ Good: Use null-conditional operators
var ssid = networkInfo?.Data?.Ssid;

// ❌ Bad: Traditional null checking
string ssid = null;
if (networkInfo != null && networkInfo.Data != null)
{
    ssid = networkInfo.Data.Ssid;
}

// ✅ Good: Use string interpolation
var message = $"Connected to {ssid}";

// ❌ Bad: String concatenation
var message = "Connected to " + ssid;

// ✅ Good: Use pattern matching
if (response.ErrorCode is WifiErrorCodes.InvalidCredential)
{
    // Handle error
}

// ✅ Good: Use expression-bodied members for simple properties
public bool IsSuccess => ErrorCode == WifiErrorCodes.Success;
```

### Async/Await

```csharp
// ✅ Good: Return Task, not void
public async Task DisconnectWifi(string ssid)
{
    await DoSomethingAsync();
}

// ❌ Bad: Async void (only for event handlers)
public async void DisconnectWifi(string ssid)
{
    await DoSomethingAsync();
}

// ✅ Good: Use ConfigureAwait(false) in library code when appropriate
var result = await SomeAsyncOperation().ConfigureAwait(false);

// ✅ Good: Support CancellationToken
public async Task<T> OperationAsync(CancellationToken cancellationToken = default)
{
    cancellationToken.ThrowIfCancellationRequested();
    // ...
}
```

### Exception Handling

```csharp
// ✅ Good: Specific exceptions
catch (ArgumentNullException ex)
{
    WifiLogger.LogError("Argument was null", ex);
    throw;
}

// ✅ Good: Don't catch and ignore
catch (Exception ex)
{
    WifiLogger.LogError("Operation failed", ex);
    return ErrorResponse(WifiErrorCodes.UnknownError, ex.Message);
}

// ❌ Bad: Empty catch blocks
catch (Exception ex)
{
    // Do nothing
}
```

## Documentation

### XML Documentation

All public APIs must have XML documentation:

```csharp
/// <summary>
/// Connects to a Wi-Fi network with the specified SSID and password.
/// </summary>
/// <param name="ssid">The Service Set Identifier (SSID) of the Wi-Fi network.</param>
/// <param name="password">The password for the Wi-Fi network.</param>
/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
/// <returns>A task that represents the asynchronous operation with the connection response.</returns>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="ssid"/> is null or empty.</exception>
/// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
/// <example>
/// <code>
/// var response = await CrossWifiManager.Current.ConnectWifi("MyNetwork", "password123");
/// if (response.IsSuccess)
/// {
///     Console.WriteLine($"Connected to {response.Data?.Ssid}");
/// }
/// </code>
/// </example>
public async Task<WifiManagerResponse<NetworkData>> ConnectWifi(
    string ssid, 
    string password, 
    CancellationToken cancellationToken = default)
{
    // Implementation
}
```

### Comments

```csharp
// ✅ Good: Explain WHY, not WHAT
// Android 10+ doesn't allow programmatic Wi-Fi disable, so open settings instead
if (OperatingSystem.IsAndroidVersionAtLeast(29))
{
    OpenWifiSettings();
}

// ❌ Bad: Stating the obvious
// Set the SSID
networkData.Ssid = ssid;

// ✅ Good: Document complex logic
// Calculate IP address from byte array in network byte order
// Format: (byte[3] << 24) | (byte[2] << 16) | (byte[1] << 8) | byte[0]
int ipAddress = GetIpAddressFromBytes(bytes);
```

## Code Organization

### File Structure

```csharp
// 1. Using statements (sorted, System first)
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MauiWifiManager.Abstractions;
using MauiWifiManager.Helpers;

// 2. Namespace
namespace MauiWifiManager
{
    // 3. Class documentation
    /// <summary>
    /// ...
    /// </summary>
    public class WifiNetworkService : IWifiNetworkService
    {
        // 4. Fields (private, protected, public)
        private static Context _Context;
        
        // 5. Constructors
        public WifiNetworkService()
        {
        }
        
        // 6. Properties
        public bool IsEnabled { get; set; }
        
        // 7. Methods (public, protected, private)
        public async Task<bool> ConnectAsync()
        {
        }
        
        private void Helper()
        {
        }
    }
}
```

### Method Length

- Keep methods short and focused (ideally < 50 lines)
- Extract complex logic into helper methods
- Use meaningful method names that describe the action

```csharp
// ✅ Good: Short, focused methods
public async Task<NetworkData> GetNetworkInfoAsync()
{
    ValidateState();
    var data = await FetchNetworkDataAsync();
    return MapToNetworkData(data);
}

// ❌ Bad: One giant method doing everything
public async Task<NetworkData> GetNetworkInfoAsync()
{
    // 200 lines of code...
}
```

## Platform-Specific Code

### Use Conditional Compilation

```csharp
#if ANDROID
    // Android-specific code
    WifiNetworkService.Init(context);
#elif IOS || MACCATALYST
    // iOS/Mac-specific code
    NEHotspotHelper.Init();
#elif WINDOWS
    // Windows-specific code
    WiFiAdapter.RequestAccessAsync();
#endif
```

### Separate Platform Files

- Use `.android.cs`, `.apple.cs`, `.windows.cs` suffixes
- Implement the same interface across all platforms
- Document platform-specific behavior in XML comments

## Error Handling

### Use Response Pattern

```csharp
// ✅ Good: Return response objects with error information
public async Task<WifiManagerResponse<NetworkData>> ConnectWifi(string ssid, string password)
{
    try
    {
        var data = await ConnectInternalAsync(ssid, password);
        return WifiManagerResponse<NetworkData>.SuccessResponse(data);
    }
    catch (ArgumentException ex)
    {
        return WifiManagerResponse<NetworkData>.ErrorResponse(
            WifiErrorCodes.InvalidCredential, 
            ex.Message);
    }
}

// ❌ Bad: Throw exceptions for expected errors
public async Task<NetworkData> ConnectWifi(string ssid, string password)
{
    if (string.IsNullOrEmpty(ssid))
    {
        throw new ArgumentException("SSID is required");
    }
}
```

## Logging

### Use WifiLogger

```csharp
// ✅ Good: Use appropriate log levels
WifiLogger.LogInfo("Wi-Fi scan started");
WifiLogger.LogWarning("Network not found");
WifiLogger.LogError("Connection failed", exception);
WifiLogger.LogDebug("Internal state: connecting");

// ❌ Bad: Direct Debug.WriteLine
Debug.WriteLine("Something happened");
```

## Testing

### Unit Test Naming

```csharp
[Fact]
public void ConnectWifi_WithInvalidSsid_ReturnsInvalidCredentialError()
{
    // Arrange
    var service = new WifiNetworkService();
    
    // Act
    var result = await service.ConnectWifi(null, "password");
    
    // Assert
    Assert.Equal(WifiErrorCodes.InvalidCredential, result.ErrorCode);
}
```

### Test Structure

- Use Arrange-Act-Assert pattern
- One assertion per test (when possible)
- Test both success and failure paths
- Mock external dependencies

## Performance

### Avoid Allocations

```csharp
// ✅ Good: Reuse collections
private static readonly char[] _TrimChars = new char[] { '"', '\"' };

// ❌ Bad: Create new array each time
ssid.Trim(new char[] { '"', '\"' });
```

### Use Async Efficiently

```csharp
// ✅ Good: Don't await unnecessarily
public Task<bool> OpenSettingsAsync()
{
    return LaunchUrlAsync(url);
}

// ❌ Bad: Unnecessary await
public async Task<bool> OpenSettingsAsync()
{
    return await LaunchUrlAsync(url);
}
```

## Git Commits

### Commit Messages

```
# ✅ Good: Clear, descriptive
Add CancellationToken support to async methods

Updated all async methods in IWifiNetworkService to accept
optional CancellationToken parameters, allowing operations
to be cancelled by the caller.

# ❌ Bad: Vague
Fixed stuff
Updated code
```

### Commit Size

- Keep commits focused and atomic
- One logical change per commit
- Commit working code that compiles

## Code Review Checklist

Before submitting a PR, ensure:

- [ ] Code follows style guidelines
- [ ] All public APIs have XML documentation
- [ ] Tests are included for new features
- [ ] No compiler warnings
- [ ] Code compiles on all target platforms
- [ ] Breaking changes are documented
- [ ] CHANGELOG.md is updated
- [ ] Error handling is appropriate
- [ ] Logging is added where needed
- [ ] Input validation is included

## Resources

- [C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [.NET API Design Guidelines](https://docs.microsoft.com/en-us/dotnet/standard/design-guidelines/)
- [Async/Await Best Practices](https://docs.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming)
