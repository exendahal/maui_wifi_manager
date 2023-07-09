using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Plugin.MauiWifiManager;

namespace Demo
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        public static Context AppContext { get; private set; }
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            AppContext = ApplicationContext;
            WifiNetworkService.Init(this);
        }
    }
}