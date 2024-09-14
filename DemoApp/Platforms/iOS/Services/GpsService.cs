using CoreLocation;
using DemoApp.Services.Interfaces;

namespace DemoApp.Platforms
{
    public class GpsService : IGpsService
    {
        public Task<bool> GpsStatus()
        {
            if (CLLocationManager.LocationServicesEnabled)
            {
                if (CLLocationManager.Status == CLAuthorizationStatus.Authorized || CLLocationManager.Status == CLAuthorizationStatus.AuthorizedAlways || CLLocationManager.Status == CLAuthorizationStatus.AuthorizedWhenInUse)
                    return Task.FromResult(true);
                else if (CLLocationManager.Status == CLAuthorizationStatus.Denied)
                    return Task.FromResult(false);
                else
                    return Task.FromResult(false);
            }
            else
                return Task.FromResult(false);

        }
    }
}
