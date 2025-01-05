
namespace DemoApp
{
    public partial class NearbyDevicesPermissionService : INearbyDevicesPermissionService
    {
#if ANDROID

#else
        public Task<PermissionStatus> CheckBlePermission()
        {
            return Task.FromResult(PermissionStatus.Granted);
        }
#endif
    }
}
