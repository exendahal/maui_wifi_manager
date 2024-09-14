using System;

namespace Plugin.MauiWifiManager
{
    /// <summary>
    /// Interface for WiFiNetworkService
    /// </summary>
    public static class CrossWifiManager
    {
        static Lazy<IWifiNetworkService> implementation = new Lazy<IWifiNetworkService>(() => CreateWifiManager(), System.Threading.LazyThreadSafetyMode.PublicationOnly);

        /// <summary>
        /// Gets if the plugin is supported on the current platform.
        /// </summary>
        public static bool IsSupported => implementation.Value == null ? false : true;

        /// <summary>
        /// Current plugin implementation to use
        /// </summary>
        public static IWifiNetworkService Current
        {
            get
            {
                IWifiNetworkService ret = implementation.Value;
                if (ret == null)
                {
                    throw NotImplementedInReferenceAssembly();
                }
                return ret;
            }
        }

        static IWifiNetworkService CreateWifiManager()
        {
#if NETSTANDARD1_0 || NETSTANDARD2_0
            return null;
#else
            return new WifiNetworkService();
#endif
        }

        internal static Exception NotImplementedInReferenceAssembly()
        {
            return new NotImplementedException("This functionality is not implemented in the portable version of this assembly.  You should reference the NuGet package from your main application project in order to reference the platform-specific implementation.");
        }

        /// <summary>
        /// Dispose of everything 
        /// </summary>
        public static void Dispose()
        {
            if (implementation != null && implementation.IsValueCreated)
            {
                implementation.Value.Dispose();
                implementation = new Lazy<IWifiNetworkService>(() => CreateWifiManager(), System.Threading.LazyThreadSafetyMode.PublicationOnly);
            }
        }
    }
}

