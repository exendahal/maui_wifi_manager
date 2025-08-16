using Microsoft.Maui.Hosting;
using Microsoft.Maui.LifecycleEvents;
using System;
namespace MauiWifiManager
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
            return new WifiNetworkService();
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
    public static class Initialize
    {
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

