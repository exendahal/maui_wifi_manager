# WifiManager.MacVendors

Names the manufacturer of a network adapter from its MAC address, offline, using the IEEE MA-L
registry (about 40,000 prefixes).

- **No dependencies.** Targets .NET Standard 2.0, so it works on any .NET platform: .NET Framework
  4.6.1+, .NET 5 to 10, .NET MAUI, WPF, WinForms, ASP.NET Core, Unity and Xamarin.
- **Offline.** The registry ships inside the package (337 KB); nothing is downloaded.
- **Display-ready names.** Legal suffixes, leading Chinese city names and all-caps styling are
  removed, so `SHENZHEN TP-LINK TECHNOLOGIES CO.,LTD` reads as `TP-Link Technologies`.

## Install

```shell
dotnet add package WifiManager.MacVendors
```

## Use

```csharp
using WifiManager.MacVendors;

string? vendor = MacVendorRegistry.Lookup("3C:CD:57:73:6E:E2"); // "Xiaomi Mobile Software"
```

Every common MAC format works:

| Input | Result |
|---|---|
| `3C:CD:57:73:6E:E2` | `Xiaomi Mobile Software` |
| `3c-cd-57-73-6e-e2` | `Xiaomi Mobile Software` |
| `3ccd.5773.6ee2` | `Xiaomi Mobile Software` |
| `3CCD57736EE2` | `Xiaomi Mobile Software` |
| `02:1B:63:84:45:E6` | `null`: a private (randomized) address, which no manufacturer owns |
| `00:1B:63` or `not a mac` | `null`: not a 6-byte MAC address |

`MacVendorRegistry.Count` gives the number of prefixes in the registry.

## Notes

- **First lookup.** The registry is read on the first lookup, in tens of milliseconds, on whichever
  thread asks first. In a UI app, make that first call off the UI thread, e.g.
  `await Task.Run(() => MacVendorRegistry.Lookup(mac))`. Later lookups are a dictionary read and safe
  from any thread.
- **Private addresses.** Phones and laptops often use a randomized MAC per network. These have the
  "locally administered" bit set and always return `null`.
- **Coverage.** The registry is IEEE's MA-L (large block) list, which covers the vast majority of
  consumer devices. The smaller MA-M and MA-S blocks are not included.

## With WifiManager.Maui

[WifiManager.Maui](https://www.nuget.org/packages/WifiManager.Maui) does not bundle the registry, to
stay small. Plug this package in once at startup to fill its `MacVendor.Lookup` and the `Vendor`
properties of `LanDeviceDetails` and `HotspotClient`:

```csharp
using MauiWifiManager.Abstractions; // MacVendor
using WifiManager.MacVendors;       // MacVendorRegistry

MacVendor.SetLookup(MacVendorRegistry.Lookup);
```

## Updating the registry

The data comes from IEEE's public registry. Maintainers refresh it with:

```shell
python tools/update_oui.py
```

then release a new version of this package. Tags ending in `-MacVendors`, e.g. `1.0.1-MacVendors`,
publish this package only.

## License

MIT. The registry data is published by the IEEE Registration Authority.
