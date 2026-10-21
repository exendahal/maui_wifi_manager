using DemoApp.Services.Interfaces;

namespace DemoApp;

/// <summary>
/// Android, iOS and Mac Catalyst only return SSIDs, BSSIDs and scan results when location is on and granted.
/// </summary>
public static class LocationAccess
{
    /// <summary>Returns null when Wi-Fi details can be read, otherwise a message describing what is missing.</summary>
    public static async Task<string?> CheckAsync()
    {
        if (DeviceInfo.Current.Platform == DevicePlatform.WinUI)
            return null;

        var gps = IPlatformApplication.Current?.Services.GetService<IGpsService>();
        if (gps != null && !await gps.GpsStatus())
            return "Location is turned off. Turn it on to read Wi-Fi network names.";

        var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        return status == PermissionStatus.Granted
            ? null
            : "Location permission is needed to read Wi-Fi network names.";
    }
}
