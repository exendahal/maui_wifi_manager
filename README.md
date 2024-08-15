# Wifi Manager for .NET MAUI

Welcome to the documentation for the MAUI Wi-Fi Manager library, a comprehensive solution designed specifically for MAUI. This library empowers developers to effortlessly manage Wi-Fi networks within their cross-platform applications. With its intuitive APIs, you can seamlessly integrate Wi-Fi functionality, allowing users to connect to, add, and retrieve information about Wi-Fi networks with ease.

[![WifiManager.Maui](https://img.shields.io/nuget/v/WifiManager.Maui)](https://www.nuget.org/packages/WifiManager.Maui/)


## Platform Support

| S. No. | Platform     |  Support  |    Remarks  |
| ------ | ------------ | --------- | ----------- |
| 1.     | Android      | &#9745;   |             |
| 2.     | iOS          | &#9745;   |             |
| 3.     | Windows      | &#9745;   |             |
| 4.     | Mac          | &#x2612;  | Coming Soon |
| 5.     | Tizen        | &#x2612;  | Coming Soon |

## Features

- Connect Wi-Fi with SSID and Password: Enable users to connect to Wi-Fi networks by providing the SSID and password.
- Add a New Wi-Fi Network: Seamlessly add new Wi-Fi networks to the device.
- Get Current Network Info: Retrieve information about the currently connected Wi-Fi network.
- Disconnect Wi-Fi: Allow users to disconnect from a Wi-Fi network.
- Open Wi-Fi Setting: Provide a quick way for users to access their device's Wi-Fi settings.

## Getting started

### Initialization

Before using the MAUI Wi-Fi Manager plugin in your application, it's essential to initialize it. Here's how to do it on different platforms:

#### Android

To use the MAUI Wi-Fi Manager on Android, you must initialize the plugin. Add the following code to your Android application:

```csharp
 WifiNetworkService.Init(this);
```

Android Permissions

```xml
<uses-permission android:name="android.permission.ACCESS_WIFI_STATE" />
<uses-permission android:name="android.permission.CHANGE_WIFI_STATE" />
<uses-permission android:name="android.permission.CHANGE_NETWORK_STATE" />
<uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
<uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" />
<uses-permission android:name="android.permission.ACCESS_COARSE_LOCATION" />
```

Make sure you request location before scanning Wi-Fi

#### iOS

Add wifi-info and HotspotConfiguration in Entitlements.plist

```xml
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
 <key>com.apple.developer.networking.wifi-info</key>
 <true/>
 <key>com.apple.developer.networking.HotspotConfiguration</key>
 <true/>
</dict>
</plist>
```

Request location permission on info.plist

```xml
<key>NSLocationWhenInUseUsageDescription</key>
<string>App want to access location to get ssid</string>
```

Example

```csharp
PermissionStatus status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
if (status == PermissionStatus.Granted)
{
    var response = await CrossWifiManager.Current.ScanWifiNetworks();    
}
else
    await DisplayAlert("No location permission", "Please provide location permission", "OK");
```

#### Connect to Wi-Fi

To connect to a Wi-Fi network programmatically, use the ConnectWifi method. This method takes the SSID and password as parameters. Here's an example:

```csharp
 var response = await CrossWifiManager.Current.ConnectWifi(ssid, password);
```

The NetworkData class is defined as follows:

```csharp
 public class NetworkData
 {
    public int StausId { get; set; }
    public string Ssid { get; set; }
    public int IpAddress { get; set; }
    public string GatewayAddress { get; set; }
    public object NativeObject { get; set; }
    public object Bssid { get; set; }
    public object SignalStrength { get; set; }
 }
```

#### Scan available Wi-Fi (Not available on iOS)

You can access available Wi-Fi networks using the ScanWifiNetworks method (Available on Android & Windows):

```csharp
   var response = await CrossWifiManager.Current.ScanWifiNetworks();
```

#### Get Wi-Fi info

You can retrieve information about the currently connected Wi-Fi network using the GetNetworkInfo method:

```csharp
 var response = await CrossWifiManager.Current.GetNetworkInfo();
```

#### Disconnect Wi-Fi

To disconnect from a Wi-Fi network, simply call the DisconnectWifi method with the SSID as a parameter:

```csharp
CrossWifiManager.Current.DisconnectWifi(ssid);
```

#### Open Wi-Fi Setting

If you want to provide users with a convenient way to access their device's Wi-Fi settings, use the OpenWifiSetting method:

```csharp
 var response = await CrossWifiManager.Current.OpenWifiSetting();
```

I value your feedback! If you encounter any issues, have suggestions for improvement, or wish to report bugs, please open an issue on GitHub or GitLab repository

## License

MIT License

Copyright (c) 2023 Santosh Dahal

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
