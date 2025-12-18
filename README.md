# Wi-Fi Manager for .NET MAUI

The Wi-Fi Manager for .NET MAUI is a simple and powerful library that helps you manage Wi-Fi networks in your cross-platform apps. With this library, you can easily connect to Wi-Fi networks, retrieve network information, and provide quick access to Wi-Fi and wireless settings.

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

- **Connect to Wi-Fi**: Connect to Wi-Fi networks using SSID and password.
- **Get Network Info**: View details about the currently connected network.
- **Disconnect Wi-Fi**: Disconnect from a specific Wi-Fi network.
- **Open Wi-Fi Settings**: Provide quick access to device Wi-Fi settings.
- **Open Wireless Settings**: Provide quick access to device wireless settings.

---

## How to Get Started

### Initialization

Before using the library, make sure to initialize it properly:

In your MauiProgram.cs, add the UseMauiWifiManager() extension method:

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

To connect to a Wi-Fi network:

```csharp
var response = await CrossWifiManager.Current.ConnectWifi("your-SSID", "your-password");
```

---

### Scan for Available Networks

To get a list of available Wi-Fi networks (Android & Windows only):

```csharp
var response = await CrossWifiManager.Current.ScanWifiNetworks();
```

---

### Get Current Network Info

To retrieve details of the currently connected Wi-Fi network:

```csharp
var response = await CrossWifiManager.Current.GetNetworkInfo();
```

---

### Disconnect Wi-Fi

To disconnect from a Wi-Fi network:

```csharp
await CrossWifiManager.Current.DisconnectWifi("your-SSID");
```

---

### Open Wireless Settings

To open the device's wireless settings:

```csharp
await CrossWifiManager.Current.OpenWirelessSetting();
```

**Note**: On iOS, this opens the app's settings instead of wireless settings.

---

### Open Wi-Fi Settings

To provide quick access to Wi-Fi settings:

```csharp
await CrossWifiManager.Current.OpenWifiSetting();
```

**Note**: On iOS, this opens the app's settings instead of Wi-Fi settings.

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

## Feature Support by Platform

| Feature                          | Android | iOS       | Windows | Notes                                   |
|----------------------------------|---------|-----------|---------|-----------------------------------------|
| Connect to Wi-Fi                 | ✅      | ✅        | ✅      | Supported on all platforms.            |
| Get Current Network Info         | ✅      | ✅        | ✅      | Supported on all platforms.            |
| Disconnect Wi-Fi                 | ✅      | ✅        | ✅      | Supported on all platforms.            |
| Scan for Available Wi-Fi Networks| ✅      | ❌        | ✅      | Not supported on iOS.                  |
| Open Wireless Settings           | ✅      | ✅*       | ✅      | *Opens app settings on iOS.            |
| Open Wi-Fi Settings              | ✅      | ✅*       | ✅      | *Opens app settings on iOS.            |

---

## Feedback & Issues

If you encounter any issues or have suggestions, please open an issue on the project's GitHub repository.

---

## Contributing Guidelines

 contributions to this project are always welcomed. To ensure a smooth collaboration, please follow these guidelines:

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
