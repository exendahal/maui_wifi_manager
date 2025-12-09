using Microsoft.Maui.Hosting;
using System;

#if ANDROID
using Microsoft.Maui.LifecycleEvents;
#endif

namespace MauiWifiManager
{
    /// <summary>
    /// Provides access to the Wi-Fi Manager service for cross-platform operations.
    /// </summary>
    public static class CrossWifiManager
    {
        private static Lazy<IWifiNetworkService> _Implementation = new(() => CreateWifiManager(), System.Threading.LazyThreadSafetyMode.PublicationOnly);
        private static WifiManagerOptions _Options = WifiManagerOptions.Default;

        private static IWifiNetworkService CreateWifiManager()
        {
            return new WifiNetworkService();
        }

        /// <summary>
        /// Gets whether the Wi-Fi Manager is supported on the current platform.
        /// </summary>
        public static bool IsSupported => _Implementation.Value != null;

        /// <summary>
        /// Gets the current Wi-Fi Manager service implementation.
        /// </summary>
        /// <exception cref="NotImplementedException">Thrown when the service is not available on the current platform.</exception>
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

        /// <summary>
        /// Gets the current Wi-Fi Manager configuration options.
        /// </summary>
        public static WifiManagerOptions Options
        {
            get => _Options;
            set => _Options = value ?? WifiManagerOptions.Default;
        }

        internal static Exception NotImplementedInReferenceAssembly()
        {
            return new NotImplementedException(
                "This functionality is not implemented in the portable version of this assembly. " +
                "You should reference the NuGet package from your main application project in order to reference the platform-specific implementation.");
        }

        /// <summary>
        /// Disposes of all Wi-Fi Manager resources and resets the service instance.
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
    /// Provides initialization methods for the Wi-Fi Manager.
    /// </summary>
    public static class Initialize
    {
        /// <summary>
        /// Registers the Wi-Fi Manager with the MAUI application builder.
        /// On Android, this also configures the necessary lifecycle events.
        /// </summary>
        /// <param name="builder">The MAUI application builder.</param>
        /// <param name="options">Optional configuration options for the Wi-Fi Manager.</param>
        /// <returns>The MAUI application builder for chaining.</returns>
        /// <example>
        /// <code>
        /// var builder = MauiApp.CreateBuilder();
        /// builder.UseMauiWifiManager(new WifiManagerOptions 
        /// { 
        ///     ConnectionTimeoutSeconds = 45,
        ///     ConnectionRetryCount = 3
        /// });
        /// </code>
        /// </example>
        public static MauiAppBuilder UseMauiWifiManager(this MauiAppBuilder builder, WifiManagerOptions? options = null)
        {
            if (options != null)
            {
                CrossWifiManager.Options = options;
            }

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

