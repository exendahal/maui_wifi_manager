# Wi-Fi Manager for .NET MAUI

The Wi-Fi Manager for .NET MAUI is a simple and powerful library that helps you manage Wi-Fi networks in your cross-platform apps. With this library, you can connect to Wi-Fi networks, retrieve network information, detect captive portals, check internet availability, and provide quick access to Wi-Fi and wireless settings.

[![WifiManager.Maui](https://img.shields.io/nuget/v/WifiManager.Maui)](https://www.nuget.org/packages/WifiManager.Maui/)

---

## Supported Platforms

| Platform  | Supported | Notes                            |
|-----------|-----------|----------------------------------|
| Android   | ✅        |                                  |
| iOS       | ✅        |                                  |
| Windows   | ✅        |                                  |
| Mac       | ⚠️        | Testing required via Mac Catalyst|
| Tizen     | ❌        |                                  |

---

## Key Features

- **Connect to Wi-Fi**: Connect using SSID and password, with support for hidden networks and WPA3-SAE.
- **Get Network Info**: View details about the currently connected network, including IPv6, DNS, subnet mask, signal strength, frequency band, channel, and link speed.
- **Check Internet Availability**: Verify whether the current Wi-Fi connection has confirmed internet access.
- **Detect Captive Portals**: Detect whether the connection is behind a login/consent portal (hotel, airport, coffee shop, etc.).
- **Observe Wi-Fi Changes**: Subscribe to connected Wi-Fi network change events.
- **Discover Networks**: Listen for Wi-Fi networks as they are discovered during a scan session.
- **Disconnect Wi-Fi**: Disconnect from a specific Wi-Fi network.
- **Open Wi-Fi Settings**: Provide quick access to device Wi-Fi settings.
- **Open Wireless Settings**: Provide quick access to device wireless settings.

---

## How to Get Started

### Initialization

Before using the library, make sure to initialize it properly:

In your MauiProgram.cs, add the `UseMauiWifiManager()` extension method:

```csharp
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiWifiManager();

        return builder.Build();
    }
}
```

Also, include these permissions in your `AndroidManifest.xml` file:

```xml
<uses-permission android:name="android.permission.ACCESS_WIFI_STATE" />
<uses-permission android:name="android.permission.CHANGE_WIFI_STATE" />
<uses-permission android:name="android.permission.CHANGE_NETWORK_STATE" />
<uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
<uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" />
<uses-permission android:name="android.permission.ACCESS_COARSE_LOCATION" />
```

Make sure to request location permissions before scanning for Wi-Fi.

#### For iOS

Add the following to your `Entitlements.plist`:

```xml
<key>com.apple.developer.networking.wifi-info</key>
<true/>
<key>com.apple.developer.networking.HotspotConfiguration</key>
<true/>
```

In `Info.plist`, request location permissions:

```xml
<key>NSLocationWhenInUseUsageDescription</key>
<string>The app needs location access to detect Wi-Fi networks.</string>
```

---

## Examples

### Connect to Wi-Fi

Basic connection using SSID and password:

```csharp
var response = await CrossWifiManager.Current.ConnectWifiAsync("your-SSID", "your-password");
```

Target a specific access point by BSSID (Android and Windows only):

```csharp
var response = await CrossWifiManager.Current.ConnectWifiAsync(
    "your-SSID", "your-password", "aa:bb:cc:dd:ee:ff");
```

---

### Connect to a Hidden Network or Use WPA3

Use `WifiConnectionOptions` to connect to a hidden SSID or specify the security type:

```csharp
using MauiWifiManager.Abstractions;

var options = new WifiConnectionOptions
{
    IsHidden = true,                        // SSID is not broadcast
    SecurityType = WifiSecurityType.Wpa3Sae // Use WPA3-SAE
};

var response = await CrossWifiManager.Current.ConnectWifiAsync(
    "your-SSID", "your-password", options);
```

Available `WifiSecurityType` values:

| Value        | Description                        |
|--------------|------------------------------------|
| `Wpa2Psk`    | WPA2-PSK (default)                 |
| `Wpa3Sae`    | WPA3-SAE (Android API 29+, iOS 15+, Windows) |
| `WpaPsk`     | WPA-PSK (TKIP)                     |
| `Open`       | No password required               |
| `Wep`        | WEP (legacy)                       |

**Platform notes:**
- Hidden network support: Android API 29+, iOS 13+, Windows.
- WPA3-SAE support: Android API 29+, iOS 15+, Windows.

---

### Scan for Available Networks

To get a one-shot list of available Wi-Fi networks (Android and Windows only):

```csharp
var response = await CrossWifiManager.Current.ScanWifiNetworksAsync();
```

---

### Discover Networks (Continuous Scan)

Use continuous scanning when you want to update your UI as networks are discovered:

```csharp
using MauiWifiManager;
using MauiWifiManager.Abstractions;

CrossWifiManager.DeviceDiscovered += (_, network) =>
{
    System.Diagnostics.Debug.WriteLine($"Discovered: {network.Ssid} ({network.Bssid})");
};

var startResponse = await CrossWifiManager.StartScanningForDevicesAsync();
if (startResponse.ErrorCode == WifiErrorCodes.Success && CrossWifiManager.IsScanning)
{
    // Scanning is active
}

// Later, stop scanning
var stopResponse = await CrossWifiManager.StopScanningAsync();
```

Notes:
- `DeviceDiscovered` is raised only while scanning is active.
- `IsScanning` tells whether a scan session is currently running.
- Continuous discovery is supported on Android and Windows.
- iOS does not support Wi-Fi network scanning APIs.

---

### Get Current Network Info

Retrieve details of the currently connected Wi-Fi network:

```csharp
var response = await CrossWifiManager.Current.GetNetworkInfoAsync();
if (response.ErrorCode == WifiErrorCodes.Success && response.Data != null)
{
    var data = response.Data;
    Console.WriteLine($"SSID:         {data.Ssid}");
    Console.WriteLine($"BSSID:        {data.Bssid}");
    Console.WriteLine($"IPv6:         {data.IPv6Address}");
    Console.WriteLine($"Subnet Mask:  {data.SubnetMask}");
    Console.WriteLine($"DNS Servers:  {string.Join(", ", data.DnsAddresses ?? [])}");
    Console.WriteLine($"RSSI:         {data.RssiDbm} dBm");        // Android only
    Console.WriteLine($"Band:         {data.FrequencyBand}");       // Android + Windows scan
    Console.WriteLine($"Channel:      {data.ChannelNumber}");       // Android + Windows scan
    Console.WriteLine($"Link Speed:   {data.LinkSpeedMbps} Mbps");  // Android only
}
```

#### `NetworkData` properties

| Property           | Type                 | Platforms              | Description                                      |
|--------------------|----------------------|------------------------|--------------------------------------------------|
| `Ssid`             | `string?`            | All                    | Network SSID                                     |
| `Bssid`            | `object?`            | All                    | Access point MAC address                         |
| `IpAddress`        | `int`                | All                    | IPv4 address (32-bit host byte order)            |
| `GatewayAddress`   | `int`                | All                    | Default gateway (32-bit host byte order)         |
| `DhcpServerAddress`| `int`                | Android, Windows       | DHCP server address                              |
| `IPv6Address`      | `string?`            | All                    | IPv6 address of the connected interface          |
| `SubnetMask`       | `string?`            | All                    | IPv4 subnet mask (e.g. `255.255.255.0`)          |
| `DnsAddresses`     | `List<string>?`      | All                    | DNS server addresses                             |
| `RssiDbm`          | `int?`               | Android                | Raw signal strength in dBm                       |
| `FrequencyBand`    | `WifiFrequencyBand`  | Android, Windows scan  | 2.4 GHz / 5 GHz / 6 GHz                         |
| `ChannelNumber`    | `int?`               | Android, Windows scan  | Wi-Fi channel number                             |
| `LinkSpeedMbps`    | `int?`               | Android                | TX link speed in Mbps                            |
| `SignalStrength`   | `object?`            | All                    | Platform-specific signal indicator               |
| `SecurityType`     | `object?`            | All                    | Security type string from the platform           |
| `NativeObject`     | `object?`            | All                    | Platform-specific native network object          |

---

### Check Internet Availability

Verify that the current Wi-Fi connection has confirmed internet access:

```csharp
var response = await CrossWifiManager.IsInternetAvailableAsync();
if (response.ErrorCode == WifiErrorCodes.Success)
{
    bool hasInternet = response.Data;
}
```

**Platform implementation:**
- **Android** — checks `NET_CAPABILITY_INTERNET` and `NET_CAPABILITY_VALIDATED` via `NetworkCapabilities`.
- **Windows** — checks `NetworkConnectivityLevel.InternetAccess` via `NetworkInformation`.
- **iOS / Mac Catalyst** — performs a DNS resolution check against `www.apple.com` with a 5-second timeout.

---

### Detect Captive Portal

Detect whether the current Wi-Fi connection is behind a login/consent portal (hotel, airport, coffee shop, etc.):

```csharp
var response = await CrossWifiManager.IsCaptivePortalDetectedAsync();
if (response.ErrorCode == WifiErrorCodes.Success && response.Data)
{
    // Prompt the user to open a browser and complete the portal login
}
```

A captive portal means the device has Wi-Fi connectivity but internet is blocked until the user authenticates through a web page. You can combine both checks to give users a precise message:

```csharp
var internet = await CrossWifiManager.IsInternetAvailableAsync();
if (internet.Data)
{
    // Full internet access
}
else
{
    var captive = await CrossWifiManager.IsCaptivePortalDetectedAsync();
    if (captive.Data)
        // "Connected to Wi-Fi, but a login page is required."
    else
        // "No internet connection."
}
```

**Platform implementation:**
- **Android** — checks `NET_CAPABILITY_CAPTIVE_PORTAL` via `NetworkCapabilities`.
- **Windows** — checks `NetworkConnectivityLevel.ConstrainedInternetAccess` or `LocalAccess`.
- **iOS / Mac Catalyst** — performs an HTTP probe to `captive.apple.com/hotspot-detect.html` and checks the response body/status.

---

### Listen for Wi-Fi Network Changes

React when the connected Wi-Fi network changes (e.g. the user switches networks from system settings):

```csharp
CrossWifiManager.WifiNetworkChanged += (_, args) =>
{
    var oldSsid = args.OldNetwork?.Ssid;
    var newSsid = args.NewNetwork?.Ssid;

    if (!string.Equals(newSsid, "your-SSID", StringComparison.Ordinal))
    {
        // Update UI / notify user
    }
};
```

---

### Disconnect Wi-Fi

```csharp
CrossWifiManager.Current.DisconnectWifi("your-SSID");
```

---

### Open Wireless Settings

```csharp
await CrossWifiManager.Current.OpenWirelessSetting();
```

**Note**: On iOS, this opens the app's settings instead of wireless settings.

---

### Open Wi-Fi Settings

```csharp
await CrossWifiManager.Current.OpenWifiSetting();
```

**Note**: On iOS, this opens the app's settings instead of Wi-Fi settings.

---

## Feature Support by Platform

| Feature                              | Android     | iOS         | Windows     | Notes                                                     |
|--------------------------------------|-------------|-------------|-------------|-----------------------------------------------------------|
| Connect to Wi-Fi                     | ✅          | ✅          | ✅          |                                                           |
| Connect — Hidden Network             | ✅ API 29+  | ✅ iOS 13+  | ✅          |                                                           |
| Connect — WPA3-SAE                   | ✅ API 29+  | ✅ iOS 15+  | ✅          |                                                           |
| Get Current Network Info             | ✅          | ✅          | ✅          |                                                           |
| IPv6 / Subnet Mask / DNS             | ✅          | ✅          | ✅          |                                                           |
| RSSI (dBm) / Link Speed              | ✅          | ❌          | ❌          | Android only via `WifiInfo`                               |
| Frequency Band / Channel             | ✅          | ❌          | ✅ scan     | From scan results on Windows; from `WifiInfo` on Android  |
| Check Internet Availability          | ✅          | ✅          | ✅          | iOS uses DNS probe                                        |
| Detect Captive Portal                | ✅          | ✅          | ✅          | iOS uses HTTP probe                                       |
| Scan for Available Networks          | ✅          | ❌          | ✅          | Not supported on iOS                                      |
| Device Discovery (Event-based)       | ✅          | ❌          | ✅          | Use `DeviceDiscovered` with Start/Stop scan methods       |
| Observe Wi-Fi Changes                | ✅          | ✅          | ✅          |                                                           |
| Disconnect Wi-Fi                     | ✅          | ✅          | ✅          |                                                           |
| Open Wi-Fi Settings                  | ✅          | ✅*         | ✅          | *Opens app settings on iOS                               |
| Open Wireless Settings               | ✅          | ✅*         | ✅          | *Opens app settings on iOS                               |

---

## Changes in Version 3.0.0

- The namespace `Plugin.MauiWifiManager` has been updated to `MauiWifiManager`.
- `NetworkData` is now part of `WifiManagerResponse`.
- Addition to `WifiManagerResponse`:

```csharp
public class WifiManagerResponse<T>
{
    public T? Data { get; set; }
    public WifiErrorCodes? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}
```

---

## Feedback & Issues

If you encounter any issues or have suggestions, please open an issue on the project's GitHub repository.

---

## Contributing Guidelines

Contributions to this project are always welcomed. To ensure a smooth collaboration, please follow these guidelines:

### **Branching Strategy**

- **`develop`**: This is the **development branch** where all new features and bug fixes should be merged. Always branch out from `develop` for your work.
- **`main`**: This is the **release branch** containing production-ready code. Only merge into `main` after thorough testing and reviews.

### **Using Issue Templates**

Create a new issue using one of the provided **issue templates**. These templates ensure we have all the necessary information to understand and address the issue effectively.

---

## Support & Motivation

Maintaining and improving this library takes time and effort.  
If you find it useful and would like to support future enhancements or ongoing maintenance, you can do so [here](https://buymemomo.com/exendahal).

Thank you for using and supporting the project.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE.md) file for details.
