using Android.App;
using Android.Content;
using Android.Net.Wifi;
using Android.OS;
using Plugin.MauiWifiManager.Abstractions;
using System;
using System.Threading.Tasks;
using static Android.Provider.Settings;
using Context = Android.Content.Context;

namespace Plugin.MauiWifiManager
{
    /// <summary>
    /// Interface for WiFiNetworkService
    /// </summary>
    /// 
    public class WifiNetworkService : IWifiNetworkService
    {
        private static Context _context;
        public WifiNetworkService() 
        {
            
        }

        public static void Init(Context context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context), "Please call WifiNetworkService.Init(this) inside the MainActivity's OnCreate function.");
            _context = context;
        }
        /// <summary>
        /// Connect Wi-Fi
        /// </summary>
        public Task<NetworkData> ConnectWifi(string ssid, string password)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Disconnect Wi-Fi
        /// From Android Q (Android 10) you can't enable/disable wifi programmatically anymore. 
        /// So, use Settings Panel to toggle wifi connectivity
        /// </summary>
        public void DisconnectWifi(string ssid)
        {
            if (_context == null)
                throw new ArgumentNullException(nameof(_context), "Please call WifiNetworkService.Init(this) inside the MainActivity's OnCreate function.");
           
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                Intent panelIntent = new Intent(Panel.ActionWifi);
                _context.StartActivity(panelIntent);
            }               
            else
            {
                WifiManager wifiManager = (WifiManager)Application.Context.GetSystemService(Context.WifiService);
                wifiManager.SetWifiEnabled(false); // Disable wifi
                wifiManager.SetWifiEnabled(true); // Enable wifi
            }
        }

        /// <summary>
        /// Get Wi-Fi Network Info
        /// </summary>
        public Task<NetworkData> GetNetworkInfo()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Open Wi-Fi Setting
        /// </summary>
        public Task<bool> OpenWifiSetting()
        {
            if (_context == null)
                throw new ArgumentNullException(nameof(_context), "Please call WifiNetworkService.Init(this) inside the MainActivity's OnCreate function.");
            var taskCompletionSource = new TaskCompletionSource<bool>();
            Intent panelIntent;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
                panelIntent = new Intent(Panel.ActionWifi);
            else
                panelIntent = new Intent(ActionWifiSettings);
            _context.StartActivity(panelIntent);
            taskCompletionSource.TrySetResult(true);
            return taskCompletionSource.Task;
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {

        }
    }
}
