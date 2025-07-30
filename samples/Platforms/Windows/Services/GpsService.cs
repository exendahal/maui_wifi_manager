using DemoApp.Services.Interfaces;
using Windows.Devices.Geolocation;

namespace DemoApp.Platforms
{
    public class GpsService : IGpsService
    {
        public async Task<bool> GpsStatus()
        {
            try
            {
                var accessStatus = await Geolocator.RequestAccessAsync();
                if (accessStatus != GeolocationAccessStatus.Allowed)
                    return false;

                var geolocator = new Geolocator();
                var pos = await geolocator.GetGeopositionAsync();
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
