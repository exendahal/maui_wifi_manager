# Wifi Manager for MAUI & Xamarin Forms
The MAUI Wi-Fi Manager library is a comprehensive solution designed specifically for MAUI & Xamarin.Forms applications, enabling easy management of Wi-Fi networks. With its intuitive APIs, developers can seamlessly integrate Wi-Fi functionality into their cross-platform applications, allowing users connect to, add, and retrieve information about Wi-Fi networks effortlessly.

## Platform Support

| S. No. | Platform     |  Support  |    Remarks  |
| ------ | ------------ | --------- | ----------- |
| 1.     | Android      | &#9745;   |             |
| 2.     | iOS          | &#9745;   |             |
| 3.     | Windows      | &#9745;   |             |
| 4.     | Mac          | &#x2612;  | Coming Soon |
| 5.     | Tizen        | &#x2612;  | Coming Soon |


## Features
* Connect Wi-Fi with SSID and Password
* Add new Wi-Fi Network
* Get current Network Info
* Disconnect Wi-Fi
* Open Wi-Fi Setting

## Getting started

### Initialization
#### Android
The plugin requires to be initialized. To use a MAUI Wi-Fi Manager inside an application, Android application must initialize the plugin. 
```csharp
 WifiNetworkService.Init(this);
```

#### Connect Wi-Fi
```csharp
 var response = await CrossWifiManager.Current.ConnectWifi(ssid, password);
```

#### Get Wi-Fi info
```csharp
 var response = await CrossWifiManager.Current.GetNetworkInfo();
```

#### Disconnect Wi-Fi
```csharp
CrossWifiManager.Current.DisconnectWifi(ssid);
```

#### Open Wi-Fi Setting
```csharp
 var response = await CrossWifiManager.Current.OpenWifiSetting();
```

## Created by: Santosh Dahal
- [Twitter](https://www.twitter.com/exendahal)
- [Linkedin](https://www.linkedin.com/in/exendahal/)

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
