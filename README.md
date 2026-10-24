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
- **Wi-Fi On/Off State**: Check whether Wi-Fi is on and get notified when it changes.
- **Permission Helper**: Request every permission Wi-Fi scanning needs in one call.
- **Signal Helpers**: Convert RSSI to bars/percentage and listen for signal changes.
- **Metered Connection Detection**: Know when the connection is metered or data-limited.
- **Saved Networks**: List and forget saved networks.
- **Persistent Network Suggestions (Android)**: Save networks that Android joins automatically, even after the app closes.
- **Enterprise Wi-Fi (802.1X)**: Connect to PEAP/TTLS networks with a username and password.
- **IoT Provisioning (Local-Only Networks)**: Connect to a device's access point without internet and route app traffic through it.
- **Richer Scan Data**: Wi-Fi standard (Wi-Fi 4/5/6/7), channel width, and per-channel congestion.
- **Connection Type**: Know whether the device is on Wi-Fi, mobile data or Ethernet, and get notified when that changes.
- **Scan Readiness**: Find out whether a scan can run now, and if not, why (throttled, permission, location off, Wi-Fi off), plus an event whenever the OS finishes a scan.
- **Connect by Name Prefix and With a Timeout**: Join the strongest network whose name starts with a prefix (e.g. `SmartPlug-`), and stop waiting after a set time.
- **Turn Wi-Fi On or Off**: Switch the Wi-Fi radio (Windows, Android 9 and older; opens the Wi-Fi panel on Android 10+).
- **Hotspot**: Start a hotspot and read its name and password; list connected devices on Windows.
- **Network Diagnostics**: Ping, speed test, find devices on the local network, and browse mDNS/Bonjour services.
- **Broadcast Address and Network Prefix**: Calculated from the IP address and subnet mask.

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

Add these only if you use the matching features:

```xml
<!-- Android 13+: scanning, hotspot and RequestPermissionsAsync -->
<uses-permission android:name="android.permission.NEARBY_WIFI_DEVICES" android:usesPermissionFlags="neverForLocation" />
<!-- BrowseServicesAsync (mDNS): lets the app receive multicast answers -->
<uses-permission android:name="android.permission.CHANGE_WIFI_MULTICAST_STATE" />
<!-- Ping, speed test, custom internet check -->
<uses-permission android:name="android.permission.INTERNET" />
```

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

To find devices on the local network or browse mDNS services, also add `NSLocalNetworkUsageDescription` (and `NSBonjourServices` listing the service types you browse) to `Info.plist`. Browsing mDNS on iOS 14+ additionally requires the `com.apple.developer.networking.multicast` entitlement, which Apple grants on request.

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

### Connect by Name Prefix or With a Timeout

IoT devices often broadcast a name with a serial number, such as `SmartPlug-4F2A`. Set `IsSsidPrefix` to join the network whose name starts with the prefix, and `Timeout` to stop waiting after a set time:

```csharp
var response = await CrossWifiManager.Current.ConnectWifiAsync("SmartPlug-", "setup-password",
    new WifiConnectionOptions
    {
        IsSsidPrefix = true,
        Timeout = TimeSpan.FromSeconds(20)
    });
// response.Data.Ssid holds the full name that was joined.
// A timeout returns WifiErrorCodes.OperationTimeout.
```

`IsSsidPrefix` and `Timeout` also work with `ConnectLocalOnlyAsync` and `AddNetworkSuggestionAsync`.

**Platform notes:**
- **iOS 13+**: iOS matches the prefix itself; your app never sees the list of networks.
- **Android and Windows**: the library scans and picks the strongest matching network, so scanning permissions are needed.
- Prefix matching doesn't work for hidden networks.

---

### Scan for Available Networks

To get a one-shot list of available Wi-Fi networks (Android and Windows only):

```csharp
var response = await CrossWifiManager.Current.ScanWifiNetworksAsync();
```

When the OS refuses a new scan (Android allows 4 scans per 2 minutes; some Windows drivers abort scans), the previous results are returned and `ErrorMessage` says so. To find out ahead of time whether a scan can run:

```csharp
var ready = await CrossWifiManager.Current.CanStartScanAsync();
// ready.Data: Ready, WifiDisabled, PermissionDenied, LocationServicesDisabled or Throttled
```

To get results whenever the OS finishes any scan, including scans started by the system (this costs no scan quota):

```csharp
CrossWifiManager.Current.ScanResultsAvailable += (s, e) =>
    Debug.WriteLine($"{e.Networks.Count} networks (fresh: {e.IsFresh})");
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

CrossWifiManager.DeviceLost += (_, network) =>
{
    System.Diagnostics.Debug.WriteLine($"Lost: {network.Ssid} ({network.Bssid})");
};

// Scanning stops when the token is canceled. Without a cancelable token it stops after 30 seconds.
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
var startResponse = await CrossWifiManager.StartScanningForDevicesAsync(cts.Token);
if (startResponse.ErrorCode == WifiErrorCodes.Success && CrossWifiManager.IsScanning)
{
    // Scanning is active
}

// Later, stop scanning
var stopResponse = await CrossWifiManager.StopScanningAsync();
```

Notes:
- `DeviceDiscovered` and `DeviceLost` are raised only while scanning is active. `DeviceLost` fires when a network goes out of range or Wi-Fi is turned off.
- Android throttles Wi-Fi scans (4 per 2 minutes in foreground, about 1 per 30 minutes in background). To stay within that quota a scan session requests a fresh scan only every 30 seconds and reads cached results (which also include scans made by the system) every 2 seconds in between, so new or removed networks may take up to ~30 seconds to show. This is an OS limit and cannot be bypassed by the library; for testing it can be turned off via Developer options → "Wi-Fi scan throttling".
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
| `WifiStandard`     | `WifiStandard`       | Android 11+, Windows scan | Wi-Fi 4/5/6/7 (802.11n/ac/ax/be)              |
| `ChannelWidthMhz`  | `int?`               | Android scan           | Channel width in MHz                             |
| `BroadcastAddress` | `string?`            | All                    | IPv4 broadcast address, calculated               |
| `NetworkPrefixLength` | `int?`            | All                    | Prefix length, e.g. 24 for `255.255.255.0`       |

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

To check against your own server instead, for example on networks that block the default probes, pass a URL. Any 2xx response counts as available, and a redirect (typical of captive portals) does not:

```csharp
var response = await CrossWifiManager.IsInternetAvailableAsync(new Uri("https://example.com/health"));
```

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

### Request Permissions

Requests location (Android/iOS), nearby Wi-Fi devices (Android 13+) and Wi-Fi access (Windows). On Android it also fails when location services are turned off, since scan results stay empty in that case.

```csharp
var response = await CrossWifiManager.Current.RequestPermissionsAsync();
if (response.ErrorCode != WifiErrorCodes.Success)
    Debug.WriteLine(response.ErrorMessage);
```

On Android 13+ add this to `AndroidManifest.xml`:

```xml
<uses-permission android:name="android.permission.NEARBY_WIFI_DEVICES" android:usesPermissionFlags="neverForLocation" />
```

---

### Wi-Fi On/Off State

```csharp
var enabled = await CrossWifiManager.Current.IsWifiEnabledAsync();

CrossWifiManager.Current.WifiStateChanged += (s, e) =>
    Debug.WriteLine(e.IsEnabled ? "Wi-Fi turned on" : "Wi-Fi turned off");
```

---

### Signal Strength

```csharp
int bars = WifiSignal.GetSignalLevel(-67);        // 0–4
int percent = WifiSignal.GetSignalPercentage(-67); // 0–100

CrossWifiManager.Current.SignalStrengthChanged += (s, e) =>
    Debug.WriteLine($"{e.SignalPercentage}% ({e.RssiDbm} dBm)");
```

On Windows the event polls every 5 seconds and `RssiDbm` is `null` (only signal bars are available).

---

### Metered Connection

```csharp
var metered = await CrossWifiManager.Current.IsMeteredConnectionAsync();
if (metered.Data)
    Debug.WriteLine("Skip large downloads");
```

---

### Saved Networks

```csharp
var saved = await CrossWifiManager.Current.GetSavedNetworksAsync();
await CrossWifiManager.Current.ForgetNetworkAsync("Old-Network");
```

What each platform lists and can remove:
- **Windows**: all saved Wi-Fi profiles.
- **iOS**: networks configured by your app.
- **Android 10+**: network suggestions added by your app. Networks the user saved through the system dialog (used by `ConnectWifiAsync` on Android 11+) can only be removed by the user.

---

### Persistent Network Suggestions (Android 10+)

Saves a network without showing UI. Android joins it automatically when it's in range, even after your app closes. The first time, the user is asked to allow suggestions from your app.

```csharp
var response = await CrossWifiManager.Current.AddNetworkSuggestionAsync(
    "Office", "password", new WifiConnectionOptions { SecurityType = WifiSecurityType.Wpa2Psk });
```

---

### Enterprise Wi-Fi (802.1X)

```csharp
var response = await CrossWifiManager.Current.ConnectEnterpriseWifiAsync("eduroam", new WifiEnterpriseOptions
{
    EapMethod = WifiEapMethod.Peap,
    Phase2Method = WifiPhase2Method.Mschapv2,
    Identity = "user@university.edu",
    Password = "password",
    AnonymousIdentity = "anonymous@university.edu",
    ServerDomain = "radius.university.edu" // required on Android 11+ unless CaCertificate is set
});
```

**Platform notes:**
- **Android**: PEAP, TTLS and PWD. Optional `CaCertificate` validates the server. On Android 10+ the network is saved as a suggestion, so the user may have to allow it before it connects.
- **iOS**: PEAP and TTLS. `ServerDomain` is used as the trusted server name.
- **Windows**: Windows chooses the EAP method itself; only `Identity` and `Password` are used.

---

### IoT Provisioning (Local-Only Networks)

Use this to connect to a device's setup access point that has no internet. Your app's HTTP calls then go to the device instead of falling back to cellular. Release the network when you're done, and the phone returns to its previous network.

```csharp
var response = await CrossWifiManager.Current.ConnectLocalOnlyAsync(
    "SmartPlug-1234", "setup-password", new WifiConnectionOptions());

if (response.ErrorCode == WifiErrorCodes.Success)
{
    using var http = new HttpClient();
    await http.PostAsync("http://192.168.4.1/provision", content);
}

await CrossWifiManager.Current.ReleaseLocalOnlyAsync();
```

**Platform notes:**
- **Android 10+**: The system shows a dialog asking the user to approve the connection, and the app's traffic is bound to the network. On Android 6–9 it joins the network and binds traffic to it.
- **iOS**: Joins with `JoinOnce`, so the network isn't saved and iOS drops it when the app goes to the background.
- **Windows**: Joins without auto-reconnect. Releasing disconnects, and Windows then reconnects to its preferred network.

---

### Richer Scan Data and Channel Congestion

Scan results now include `WifiStandard` (Wi-Fi 4/5/6/7), plus `ChannelWidthMhz` on Android. Windows scan results also include `RssiDbm`.

```csharp
var scan = await CrossWifiManager.Current.ScanWifiNetworksAsync();
foreach (var channel in WifiChannelUsage.FromScanResults(scan.Data!))
    Debug.WriteLine($"{channel.FrequencyBand} ch {channel.ChannelNumber}: {channel.NetworkCount} networks");
```

---

### Connection Type

```csharp
var types = await CrossWifiManager.Current.GetConnectionTypesAsync();
// types.Data: e.g. [WiFi] or [Cellular]; empty when offline

CrossWifiManager.Current.ConnectionTypeChanged += (s, e) =>
    Debug.WriteLine($"{string.Join(", ", e.ConnectionTypes)} internet={e.HasInternet}");
```

Built on .NET MAUI's `Connectivity`, so it works on every platform.

---

### Turn Wi-Fi On or Off

```csharp
var response = await CrossWifiManager.Current.SetWifiEnabledAsync(false);
if (response.ErrorCode == WifiErrorCodes.Success && !response.Data)
{
    // Android 10+ doesn't let apps switch Wi-Fi; the system Wi-Fi panel was opened for the user instead.
}
```

Supported on Windows and Android 9 and older. Not available on iOS.

---

### Hotspot

```csharp
var hotspot = await CrossWifiManager.Current.StartHotspotAsync();
if (hotspot.ErrorCode == WifiErrorCodes.Success)
    Debug.WriteLine($"Join {hotspot.Data!.Ssid} with password {hotspot.Data.Password}");

var clients = await CrossWifiManager.Current.GetHotspotClientsAsync(); // Windows only
await CrossWifiManager.Current.StopHotspotAsync();
```

**Platform notes:**
- **Android 8+**: starts a *local-only* hotspot. Other devices can connect to the phone (handy for sending setup data to an IoT device), but the phone's internet isn't shared. Android picks the name and password. Needs location permission (Android 12 and older) or nearby Wi-Fi devices permission (Android 13+).
- **Windows**: turns on Mobile Hotspot and shares the current internet connection, using the name and password set in Windows Settings.
- **iOS**: not available.

---

### MAC Manufacturer Lookup

Naming a device's manufacturer from its MAC address needs the IEEE MA-L registry (about 40,000
prefixes). It ships as a separate package, [`WifiManager.MacVendors`](https://www.nuget.org/packages/WifiManager.MacVendors),
so `WifiManager.Maui` stays small for apps that don't use it. The data package has no dependencies and
works on any .NET platform, MAUI or not.

```xml
<PackageReference Include="WifiManager.MacVendors" Version="1.0.0" />
```

Plug it in once at startup:

```csharp
using MauiWifiManager.Abstractions;
using WifiManager.MacVendors;

builder.UseMauiWifiManager();
MacVendor.SetLookup(MacVendorRegistry.Lookup);

MacVendor.Lookup("3C:CD:57:73:6E:E2");       // "Xiaomi Mobile Software"
details.Data?.Vendor;                        // filled in for LanDeviceDetails and HotspotClient
```

Outside MAUI, use the registry directly: `MacVendorRegistry.Lookup("3C:CD:57:73:6E:E2")`.

**Notes:**
- Private (randomized) addresses, unknown prefixes and malformed input return `null`.
- The registry (337 KB) loads on the first lookup, in tens of milliseconds, so make that first call off the UI thread.
- To use your own copy or another source, pass any `Func<string, string?>` to `MacVendor.SetLookup`.
- Maintainers: refresh the registry from IEEE with `python tools/update_oui.py`; it rewrites
  `src/WifiManager.MacVendors/Resources/oui.tsv.gz`, which is also in the repository for anyone who wants the raw data.

### Network Diagnostics

```csharp
// Ping
var ping = await CrossWifiManager.Current.PingAsync("one.one.one.one", count: 4);
Debug.WriteLine($"avg {ping.Data?.AverageMs} ms, loss {ping.Data?.PacketLossPercent}%");

// Speed test (Cloudflare's public endpoints by default; transfers up to ~30 MB)
var speed = await CrossWifiManager.Current.MeasureSpeedAsync();
Debug.WriteLine($"down {speed.Data?.DownloadMbps} Mbps, up {speed.Data?.UploadMbps} Mbps");

// Your own endpoints
var custom = await CrossWifiManager.Current.MeasureSpeedAsync(new SpeedTestOptions
{
    DownloadUrl = new Uri("https://files.example.com/10MB.bin"),
    UploadUrl = null // skip upload
});

// Devices on the local network (pings the subnet, at most 254 addresses)
var devices = await CrossWifiManager.Current.DiscoverDevicesAsync();
foreach (var device in devices.Data ?? [])
    Debug.WriteLine($"{device.IpAddress} {device.HostName}");

// Or show each device as soon as it answers, instead of waiting for the whole sweep
var live = new Progress<LanDevice>(device => Debug.WriteLine($"found {device.IpAddress}"));
await CrossWifiManager.Current.DiscoverDevicesAsync(live);

// Everything one device reveals: names, MAC, manufacturer/model, open ports, web page title, ping (about 3 s)
var details = await CrossWifiManager.Current.GetDeviceDetailsAsync("192.168.1.1");
Debug.WriteLine($"{details.Data?.HttpTitle} {details.Data?.MacAddress} ports: {string.Join(", ", details.Data?.OpenPorts ?? [])}");
Debug.WriteLine($"made by {details.Data?.Vendor}"); // e.g. "Espressif", from the MAC address

// Manufacturer of any MAC address, offline (needs WifiManager.MacVendors, see below)
string? vendor = MacVendor.Lookup("3C:CD:57:73:6E:E2"); // "Xiaomi Mobile Software"

// mDNS / Bonjour: list service types, then the devices for one type
var types = await CrossWifiManager.Current.BrowseServicesAsync();
var printers = await CrossWifiManager.Current.BrowseServicesAsync("_ipp._tcp");
foreach (var printer in printers.Data ?? [])
    Debug.WriteLine($"{printer.Name} at {printer.IpAddress}:{printer.Port}");

// Traceroute, with each hop as it answers
var hops = new Progress<TracerouteHop>(hop =>
    Debug.WriteLine($"{hop.Hop}  {hop.Address ?? "*"}  {hop.HostName}  {hop.RoundTripMs} ms"));
var route = await CrossWifiManager.Current.TracerouteAsync("one.one.one.one", maxHops: 30, progress: hops);

// DNS lookup straight against a server (the system's by default)
var mx = await CrossWifiManager.Current.LookupDnsAsync("example.com", DnsRecordType.Mx, server: "8.8.8.8");
foreach (var record in mx.Data?.Records ?? [])
    Debug.WriteLine($"{record.Type} {record.Value} (TTL {record.TimeToLiveSeconds} s)");

// Which TCP ports are open
var ports = await CrossWifiManager.Current.CheckPortsAsync("192.168.1.1", [22, 80, 443, 8080]);
foreach (var port in ports.Data ?? [])
    Debug.WriteLine($"{port.Port} {port.Service} {(port.IsOpen ? "open" : "closed")}");

// Wake a sleeping computer
await CrossWifiManager.Current.WakeOnLanAsync("AA:BB:CC:DD:EE:FF", broadcastAddress: "192.168.1.255");
```

**Notes:**
- Device discovery only finds devices that answer ping, and on larger networks it covers only the /24 around this device.
- `GetDeviceDetailsAsync` combines reverse DNS, NetBIOS (computer name and MAC), UPnP (manufacturer and model), a check of common ports (`LanDeviceDetails.KnownPorts`), the device's web page title and ping. Devices only reveal what they answer to, so many fields can be empty.
- MAC addresses come from the ARP table on Windows. Android 10+ and iOS hide it, so there the MAC is only known when the device answers NetBIOS. `IsRandomizedMac` tells you when a device (typically a phone) uses a private address.
- `Vendor` (on `LanDeviceDetails` and `HotspotClient`) and `MacVendor.Lookup` name the adapter's manufacturer. The registry behind them is **not** in this package, so apps that don't need it don't carry it; see [MAC Manufacturer Lookup](#mac-manufacturer-lookup). Without it they return `null`.
- Android blocks plain `http://` requests by default, which stops the web page title and UPnP lookups. To allow them, add `android:usesCleartextTraffic="true"` to the `<application>` element in `AndroidManifest.xml`, or allow cleartext only for local addresses with a [network security config](https://developer.android.com/privacy-and-security/security-config).
- `TracerouteAsync` sends pings with increasing TTL. Routers that drop ICMP show as hops with a null `Address`. On Android .NET has no raw sockets and runs the system `ping` per hop, so intermediate hop times are approximate (the process start is measured and subtracted); the final hop's time is exact.
- `LookupDnsAsync` supports A, AAAA, CNAME, MX, TXT and NS. `NameNotFound` is true when the name does not exist (NXDOMAIN). Android does not expose the system DNS server to .NET, so there it asks 1.1.1.1 unless you pass `server`.
- `CheckPortsAsync` treats a port as open when a TCP connection succeeds within 800 ms. `Service` names well-known ports.
- `WakeOnLanAsync` only sends the packet; the target must have Wake-on-LAN enabled and be on the same network.
- The speed test's latency is the fastest of three requests to `SpeedTestOptions.LatencyUrl`, minus the server's own processing time from its `Server-Timing` header.
- Ping, speed test, device discovery, mDNS, traceroute, DNS lookup, port check and Wake-on-LAN are shared .NET code. They also work for custom `IWifiNetworkService` implementations.

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
| Wi-Fi On/Off State + Event           | ✅          | ❌          | ✅          | No public API on iOS                                      |
| Request Permissions                  | ✅          | ✅          | ✅          |                                                           |
| Signal Strength Event                | ✅          | ❌          | ✅          | Windows polls every 5 s                                   |
| Metered Connection                   | ✅          | ✅          | ✅          |                                                           |
| Saved Networks (list / forget)       | ✅*         | ✅*         | ✅          | *Only networks added by your app                          |
| Network Suggestions                  | ✅ API 29+  | ❌          | ❌          |                                                           |
| Enterprise Wi-Fi (802.1X)            | ✅          | ✅          | ✅          | See platform notes above                                  |
| Local-Only Network (IoT)             | ✅ API 23+  | ✅          | ✅          |                                                           |
| Wi-Fi Standard / Channel Width       | ✅          | ❌          | ✅ standard | Channel width on Android only                             |
| Connection Type + Event              | ✅          | ✅          | ✅          | Wi-Fi, mobile data, Ethernet, Bluetooth                   |
| Scan Readiness / Scan Results Event  | ✅          | ❌          | ✅          | Android reports throttling                                |
| Connect by SSID Prefix               | ✅          | ✅ iOS 13+  | ✅          | Android/Windows scan and pick the strongest match         |
| Connection Timeout                   | ✅          | ✅          | ✅          |                                                           |
| Custom Internet Check URL            | ✅          | ✅          | ✅          |                                                           |
| Turn Wi-Fi On/Off                    | ✅*         | ❌          | ✅          | *Android 10+ opens the Wi-Fi panel                        |
| Hotspot                              | ✅ API 26+  | ❌          | ✅          | Android: local-only; client list on Windows only          |
| Ping / Speed Test                    | ✅          | ✅          | ✅          |                                                           |
| Local Network Device Discovery       | ✅          | ✅          | ✅          | Devices must answer ping                                  |
| Device Details (name, MAC, model, ports) | ✅*     | ✅*         | ✅          | *MAC only via NetBIOS on Android 10+ and iOS              |
| MAC Manufacturer Lookup              | ✅          | ✅          | ✅          | Opt-in: `WifiManager.MacVendors` package                  |
| Traceroute / DNS / Port Check / Wake-on-LAN | ✅      | ✅          | ✅          | Shared .NET code                                          |
| mDNS / Bonjour Browsing              | ✅          | ✅*         | ✅          | *Needs Apple's multicast entitlement                      |
| Broadcast Address / Prefix Length    | ✅          | ✅          | ✅          |                                                           |

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
