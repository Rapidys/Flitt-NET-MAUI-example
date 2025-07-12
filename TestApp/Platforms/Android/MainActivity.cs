#if ANDROID
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using TestApp.Services;

namespace TestApp;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private GooglePayService _googlePayService;

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        
        // Initialize Google Pay service with your merchant ID
        _googlePayService = new GooglePayService(this, 1549901); // Replace with your actual merchant ID
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        
        // Handle Google Pay result
        _googlePayService?.OnActivityResult(requestCode, resultCode, data);
    }

    public GooglePayService GetGooglePayService()
    {
        return _googlePayService;
    }
}
#endif