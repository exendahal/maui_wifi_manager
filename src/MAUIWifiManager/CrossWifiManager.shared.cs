using Microsoft.Maui.Hosting;
using System;

#if ANDROID
using Microsoft.Maui.LifecycleEvents;
#endif

namespace MauiWifiManager
{
    /// <summary>
    /// Interface for WiFiNetworkService
    /// </summary>
    public static class CrossWifiManager
    {
        private static Lazy<IWifiNetworkService> _Implementation = new(() => CreateWifiManager(), System.Threading.LazyThreadSafetyMode.PublicationOnly);

        private static IWifiNetworkService CreateWifiManager()
        {
            return new WifiNetworkService();
        }

        /// <summary>
        /// Gets if the package is supported on the current platform.
        /// </summary>
        public static bool IsSupported => _Implementation.Value == null ? false : true;

        /// <summary>
        /// Current package implementation to use
        /// </summary>
        public static IWifiNetworkService Current
        {
            get
            {
                IWifiNetworkService ret = _Implementation.Value;
                if (ret == null)
                {
                    throw NotImplementedInReferenceAssembly();
                }
                return ret;
            }
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
            if (_Implementation != null && _Implementation.IsValueCreated)
            {
                _Implementation.Value.Dispose();
                _Implementation = new Lazy<IWifiNetworkService>(() => CreateWifiManager(), System.Threading.LazyThreadSafetyMode.PublicationOnly);
            }
        }
    }

    /// <summary>
    /// Initialize WifiManager
    /// </summary>
    public static class Initialize
    {
        /// <summary>
        /// Initialize WifiManager on Android
        /// </summary>
        public static MauiAppBuilder UseMauiWifiManager(this MauiAppBuilder builder)
        {
        #if ANDROID
            builder.ConfigureLifecycleEvents(events =>
            {
                events.AddAndroid(android => android.OnCreate((activity, bundle) =>
                {                    
                    WifiNetworkService.Init(Microsoft.Maui.ApplicationModel.Platform.CurrentActivity);
                }));
            });
          
        #endif
            return builder;
        }
    }
}

