using MauiWifiManager.Abstractions;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace MauiWifiManager
{
    /// <summary>
    /// Connection type and change events, built on MAUI Essentials <see cref="Microsoft.Maui.Networking.Connectivity"/>.
    /// </summary>
    internal static class ConnectionMonitor
    {
        private static readonly object _Lock = new();
        private static readonly List<(object Sender, EventHandler<ConnectionTypeChangedEventArgs> Handler)> _Handlers = new();

        public static WifiManagerResponse<List<NetworkConnectionType>> GetConnectionTypes()
        {
            try
            {
                var types = Map(Microsoft.Maui.Networking.Connectivity.Current.ConnectionProfiles);
                return WifiManagerResponse<List<NetworkConnectionType>>.SuccessResponse(types,
                    types.Count == 0 ? "Not connected." : string.Join(", ", types));
            }
            catch (Exception ex) when (ex is NotImplementedException or NotSupportedException)
            {
                return WifiManagerResponse<List<NetworkConnectionType>>.ErrorResponse(WifiErrorCodes.UnsupportedHardware, "Connection type is not available on this platform.");
            }
        }

        public static void Add(object sender, EventHandler<ConnectionTypeChangedEventArgs>? handler)
        {
            if (handler == null) return;
            lock (_Lock)
            {
                if (_Handlers.Count == 0 && !TrySubscribe())
                    return;
                _Handlers.Add((sender, handler));
            }
        }

        public static void Remove(EventHandler<ConnectionTypeChangedEventArgs>? handler)
        {
            lock (_Lock)
            {
                int index = _Handlers.FindIndex(h => h.Handler == handler);
                if (index < 0) return;
                _Handlers.RemoveAt(index);
                if (_Handlers.Count == 0)
                    Microsoft.Maui.Networking.Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
            }
        }

        private static bool TrySubscribe()
        {
            try
            {
                Microsoft.Maui.Networking.Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
                return true;
            }
            catch (Exception ex) when (ex is NotImplementedException or NotSupportedException)
            {
                return false; // No connectivity API on this target (e.g. plain net10.0).
            }
        }

        private static void OnConnectivityChanged(object? sender, Microsoft.Maui.Networking.ConnectivityChangedEventArgs e)
        {
            var args = new ConnectionTypeChangedEventArgs(Map(e.ConnectionProfiles),
                e.NetworkAccess == Microsoft.Maui.Networking.NetworkAccess.Internet);
            List<(object Sender, EventHandler<ConnectionTypeChangedEventArgs> Handler)> handlers;
            lock (_Lock) handlers = _Handlers.ToList();
            foreach (var (owner, handler) in handlers)
                handler(owner, args);
        }

        internal static List<NetworkConnectionType> Map(IEnumerable<Microsoft.Maui.Networking.ConnectionProfile> profiles) =>
            profiles.Select(p => p switch
            {
                Microsoft.Maui.Networking.ConnectionProfile.WiFi => NetworkConnectionType.WiFi,
                Microsoft.Maui.Networking.ConnectionProfile.Cellular => NetworkConnectionType.Cellular,
                Microsoft.Maui.Networking.ConnectionProfile.Ethernet => NetworkConnectionType.Ethernet,
                Microsoft.Maui.Networking.ConnectionProfile.Bluetooth => NetworkConnectionType.Bluetooth,
                _ => NetworkConnectionType.Unknown
            }).Distinct().ToList();
    }
}
