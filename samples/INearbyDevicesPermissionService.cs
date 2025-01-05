namespace DemoApp
{
    public interface INearbyDevicesPermissionService
    {
        public Task<PermissionStatus> CheckBlePermission();
    }
}
