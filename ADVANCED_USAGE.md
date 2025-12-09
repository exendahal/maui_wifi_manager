# Advanced Usage Guide

This guide covers advanced features and usage patterns for the MAUI Wi-Fi Manager library.

## Table of Contents
- [Configuration Options](#configuration-options)
- [Retry Logic](#retry-logic)
- [Input Validation](#input-validation)
- [Network Filtering](#network-filtering)
- [Cancellation Support](#cancellation-support)
- [Error Handling](#error-handling)
- [Logging](#logging)

## Configuration Options

You can customize the Wi-Fi Manager behavior by providing options during initialization:

```csharp
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiWifiManager(new WifiManagerOptions
            {
                ConnectionTimeoutSeconds = 45,      // Increase connection timeout
                ConnectionRetryCount = 3,           // Retry failed connections 3 times
                RetryDelayMilliseconds = 2000,      // Wait 2 seconds between retries
                ValidateInputs = true,              // Enable input validation
                AllowEmptyPassword = false,         // Require passwords for all networks
                MinimumSignalStrength = -60         // Filter by signal strength
            });

        return builder.Build();
    }
}
```

### Available Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `ConnectionTimeoutSeconds` | int | 30 | Timeout for connection operations |
| `ScanTimeoutSeconds` | int | 15 | Timeout for scan operations |
| `ConnectionRetryCount` | int | 0 | Number of retry attempts for failed connections |
| `RetryDelayMilliseconds` | int | 1000 | Delay between retry attempts |
| `ValidateInputs` | bool | true | Enable SSID and password validation |
| `AllowEmptyPassword` | bool | false | Allow empty passwords (for open networks) |
| `MinimumSignalStrength` | int | -70 | Minimum signal strength threshold (RSSI) |

## Retry Logic

Use the built-in retry mechanism for more reliable connections:

```csharp
using MauiWifiManager;

// Connect with automatic retries
var response = await CrossWifiManager.Current.ConnectWifiWithRetry(
    ssid: "MyNetwork",
    password: "MyPassword",
    retryCount: 3,
    retryDelayMilliseconds: 1500
);

if (response.IsSuccess)
{
    Console.WriteLine($"Connected to {response.Data?.Ssid}");
}
else
{
    Console.WriteLine($"Failed: {response.ErrorMessage}");
}
```

The retry logic automatically:
- Retries failed connections up to the specified count
- Skips retry for non-retryable errors (invalid credentials, permission denied, etc.)
- Respects cancellation tokens
- Adds delay between attempts

## Input Validation

Input validation helps catch errors early:

```csharp
// Manual validation
using MauiWifiManager.Helpers;

try
{
    WifiValidationHelper.ValidateSsid("MyNetwork");
    WifiValidationHelper.ValidatePassword("MyPassword");
    
    // Proceed with connection
    var response = await CrossWifiManager.Current.ConnectWifi("MyNetwork", "MyPassword");
}
catch (ArgumentException ex)
{
    Console.WriteLine($"Invalid input: {ex.Message}");
}
```

Validation rules:
- **SSID**: Must not be null/empty, maximum 32 characters
- **Password**: Must be 8-63 characters for WPA/WPA2 networks (unless empty passwords are allowed)

## Network Filtering

Filter and sort scanned networks:

```csharp
using MauiWifiManager;

// Scan for networks
var scanResponse = await CrossWifiManager.Current.ScanWifiNetworks();

if (scanResponse.IsSuccess && scanResponse.Data != null)
{
    var networks = scanResponse.Data;

    // Filter by signal strength (RSSI >= -60)
    var strongNetworks = networks.FilterBySignalStrength(-60);

    // Filter by SSID pattern
    var companyNetworks = networks.FilterBySsidPattern("Company");

    // Remove duplicate SSIDs (keeps strongest)
    var uniqueNetworks = networks.RemoveDuplicateSsids();

    // Sort by signal strength (strongest first)
    var sortedNetworks = networks.SortBySignalStrength();

    // Chain filters
    var filteredNetworks = networks
        .FilterBySignalStrength(-65)
        .RemoveDuplicateSsids()
        .SortBySignalStrength();

    foreach (var network in filteredNetworks)
    {
        Console.WriteLine($"{network.Ssid}: {network.SignalStrength}");
    }
}
```

## Cancellation Support

All async operations support cancellation:

```csharp
using System.Threading;

var cts = new CancellationTokenSource();

// Cancel after 10 seconds
cts.CancelAfter(TimeSpan.FromSeconds(10));

try
{
    var response = await CrossWifiManager.Current.ConnectWifi(
        "MyNetwork",
        "MyPassword",
        cts.Token
    );

    if (response.IsSuccess)
    {
        Console.WriteLine("Connected successfully");
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Connection was cancelled");
}

// Cancel from UI
private CancellationTokenSource? _connectionCts;

async Task ConnectAsync()
{
    _connectionCts = new CancellationTokenSource();
    
    var response = await CrossWifiManager.Current.ConnectWifi(
        "MyNetwork",
        "MyPassword",
        _connectionCts.Token
    );
}

void CancelConnection()
{
    _connectionCts?.Cancel();
}
```

## Error Handling

Comprehensive error handling:

```csharp
var response = await CrossWifiManager.Current.ConnectWifi("MyNetwork", "MyPassword");

switch (response.ErrorCode)
{
    case WifiErrorCodes.Success:
        Console.WriteLine($"Connected to {response.Data?.Ssid}");
        Console.WriteLine($"IP: {response.Data?.GetIpAddressString()}");
        Console.WriteLine($"Gateway: {response.Data?.GetGatewayAddressString()}");
        break;

    case WifiErrorCodes.InvalidCredential:
        Console.WriteLine("Invalid password. Please try again.");
        break;

    case WifiErrorCodes.PermissionDenied:
        Console.WriteLine("Location permission is required. Please grant permission.");
        break;

    case WifiErrorCodes.NetworkUnavailable:
        Console.WriteLine("Network not found. Check if you're in range.");
        break;

    case WifiErrorCodes.OperationTimeout:
        Console.WriteLine("Connection timed out. Try again.");
        break;

    case WifiErrorCodes.WifiNotEnabled:
        Console.WriteLine("Wi-Fi is disabled. Please enable Wi-Fi.");
        break;

    default:
        Console.WriteLine($"Error: {response.ErrorMessage}");
        break;
}
```

## Logging

The library uses a centralized logging system. In DEBUG mode, logs are written to the debug output:

```
[WifiManager:INFO] Wi-Fi Scan started.
[WifiManager:INFO] Wi-Fi Scan complete.
[WifiManager:WARNING] Connection failed: NetworkUnavailable
[WifiManager:ERROR] Failed to get Wi-Fi adapter.
```

To integrate with your own logging system, you can wrap the calls and log based on the response:

```csharp
using Microsoft.Extensions.Logging;

public class WifiService
{
    private readonly ILogger<WifiService> _logger;

    public WifiService(ILogger<WifiService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> ConnectToNetworkAsync(string ssid, string password)
    {
        _logger.LogInformation("Attempting to connect to {Ssid}", ssid);

        var response = await CrossWifiManager.Current.ConnectWifi(ssid, password);

        if (response.IsSuccess)
        {
            _logger.LogInformation("Successfully connected to {Ssid}", ssid);
            return true;
        }
        else
        {
            _logger.LogError("Failed to connect to {Ssid}: {Error}", ssid, response.ErrorMessage);
            return false;
        }
    }
}
```

## Utility Methods

### Check Connection Status

```csharp
// Quick check if connected to Wi-Fi
bool isConnected = await CrossWifiManager.Current.IsConnectedToWifi();

if (isConnected)
{
    string? currentSsid = await CrossWifiManager.Current.GetCurrentSsid();
    Console.WriteLine($"Connected to: {currentSsid}");
}
```

### Get Network Information

```csharp
var networkInfo = await CrossWifiManager.Current.GetNetworkInfo();

if (networkInfo.IsSuccess && networkInfo.Data != null)
{
    var data = networkInfo.Data;
    
    Console.WriteLine($"SSID: {data.Ssid}");
    Console.WriteLine($"BSSID: {data.Bssid}");
    Console.WriteLine($"IP Address: {data.GetIpAddressString()}");
    Console.WriteLine($"Gateway: {data.GetGatewayAddressString()}");
    Console.WriteLine($"DHCP Server: {data.GetDhcpServerAddressString()}");
    Console.WriteLine($"Signal Strength: {data.SignalStrength}");
    Console.WriteLine($"Security Type: {data.SecurityType}");
}
```

## Best Practices

1. **Always check `IsSuccess`** before accessing `Data` in the response
2. **Use cancellation tokens** for long-running operations
3. **Enable input validation** in production to catch errors early
4. **Implement retry logic** for better reliability
5. **Request permissions** before calling Wi-Fi operations (especially on Android)
6. **Handle all error codes** appropriately in your UI
7. **Filter duplicate SSIDs** when displaying scan results to users
8. **Sort by signal strength** to show the best networks first

## Platform-Specific Considerations

### Android
- Requires location permissions for Wi-Fi scanning
- Behavior differs based on Android version (Q and above have restrictions)
- Use `DisconnectWifi()` to open Wi-Fi settings on Android 10+

### iOS
- Scanning is not supported (API limitation)
- Opening settings navigates to app settings, not Wi-Fi settings
- Requires entitlements in `Entitlements.plist`

### Windows
- Full feature support including scanning
- May require administrator privileges for some operations
- Uses the Windows Wi-Fi API

## Examples

For complete working examples, see the `/samples` directory in the repository.
