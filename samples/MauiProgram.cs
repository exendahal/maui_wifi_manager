using CommunityToolkit.Maui;
using DemoApp.Platforms;
using DemoApp.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using ZXing.Net.Maui.Controls;
using MauiWifiManager;

namespace DemoApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseBarcodeReader()
                .UseMauiCommunityToolkit()
                .UseMauiWifiManager()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif
#if ANDROID
            // Form fields sit inside cards with their own dividers, so drop the Material underline.
            var noUnderline = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoUnderline", (h, _) => h.PlatformView.BackgroundTintList = noUnderline);
            Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("NoUnderline", (h, _) => h.PlatformView.BackgroundTintList = noUnderline);
#endif
#if WINDOWS
            // MAUI hides the On/Off text of the WinUI ToggleSwitch but keeps its 154px minimum width; drop it so the toggle aligns right.
            Microsoft.Maui.Handlers.SwitchHandler.Mapper.AppendToMapping("Compact", (h, _) => h.PlatformView.MinWidth = 0);
            // Windows 11 Mica backdrop; pages are transparent on Windows so it shows through.
            builder.ConfigureLifecycleEvents(events => events.AddWindows(windows =>
                windows.OnWindowCreated(window => window.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop())));
#endif
            builder.Services.AddTransient<IGpsService, GpsService>();
            return builder.Build();
        }
    }
}
