using Microsoft.Extensions.Logging;
using TestApp.Services;
#if ANDROID
// using TestApp.Platforms.Android.Services;
#endif

namespace TestApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Register Google Pay service
#if ANDROID
        builder.Services.AddSingleton<IGooglePayService, GooglePayServiceWrapper>();
#else
        builder.Services.AddSingleton<IGooglePayService>(provider => null);
#endif

        builder.Services.AddTransient<TestApp.Pages.PaymentPage>();
        return builder.Build();
    }
}