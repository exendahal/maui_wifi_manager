using Plugin.XFWifiManager.Abstractions;
using System;

namespace Plugin.XFWifiManager
{
    /// <summary>
    /// Cross Feature1
    /// </summary>
    public static class CrossFeature1
    {
        static Lazy<IWiFiNetworkService> implementation = new Lazy<IWiFiNetworkService>(() => CreateFeature1(), System.Threading.LazyThreadSafetyMode.PublicationOnly);

        /// <summary>
        /// Gets if the plugin is supported on the current platform.
        /// </summary>
        public static bool IsSupported => !(implementation.Value == null);

        /// <summary>
        /// Current plugin implementation to use
        /// </summary>
        public static IWiFiNetworkService Current
        {
            get
            {
                IWiFiNetworkService ret = implementation.Value;
                if (ret == null)
                {
                    throw NotImplementedInReferenceAssembly();
                }
                return ret;
            }
        }

        static IWiFiNetworkService CreateFeature1()
        {
#if NETSTANDARD1_0 || NETSTANDARD2_0
            return null;
#else
#pragma warning disable IDE0022 // Use expression body for methods
            return new WiFiNetworkService();
#pragma warning restore IDE0022 // Use expression body for methods
#endif
        }

        internal static Exception NotImplementedInReferenceAssembly() =>
            new NotImplementedException("This functionality is not implemented in the portable version of this assembly.  You should reference the NuGet package from your main application project in order to reference the platform-specific implementation.");

    }
}
