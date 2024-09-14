using Android.Content;
using DemoApp.Services.Interfaces;

namespace DemoApp.Platforms
{
    public class GpsService : IGpsService
    {
        public async Task<bool> GpsStatus()
        {
            global::Android.Locations.LocationManager manager = (Android.Locations.LocationManager)Android.App.Application.Context.GetSystemService(Context.LocationService);
            if (manager.IsProviderEnabled(Android.Locations.LocationManager.GpsProvider))
                return true;
            else
            {
                try
                {
                    //This is not checked for Android 6
                    return manager.IsLocationEnabled;
                }
                catch
                {
                    
                }
                return false;
            }
        }
    }
}
