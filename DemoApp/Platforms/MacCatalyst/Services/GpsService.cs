using DemoApp.Services.Interfaces;

namespace DemoApp.Platforms
{
    public class GpsService : IGpsService
    {
        public Task<bool> GpsStatus()
        {
            return Task.FromResult(true);
        }
    }
}
