using Android;
using Android.OS;

namespace DemoApp
{
    public partial class NearbyDevicesPermissionService : INearbyDevicesPermissionService
    {
        public async Task<PermissionStatus> CheckBlePermission()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
                return await Permissions.RequestAsync<BLEPermission>();
            else
                return PermissionStatus.Granted;
        }
    }

    public class BLEPermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions => new List<(string androidPermission, bool isRuntime)>
            {
                (Manifest.Permission.NearbyWifiDevices, true)
            }.ToArray();
    }
}
