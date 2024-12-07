using CommunityToolkit.Maui;
using DemoApp.Platforms;
using DemoApp.Services.Interfaces;
using Microsoft.Extensions.Logging;
using ZXing.Net.Maui.Controls;

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
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif
            builder.Services.AddTransient<IGpsService, GpsService>();
            return builder.Build();
        }
    }
}
