namespace DemoApp.Services.Interfaces
{
    public interface IGpsService
    {
        Task<bool> GpsStatus();
    }

}
