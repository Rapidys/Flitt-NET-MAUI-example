using TestApp.Services;

namespace TestApp;

public partial class MainPage : ContentPage
{
    int count = 0;

    public MainPage()
    {
        InitializeComponent();
    }

    private void OnCounterClicked(object sender, EventArgs e)
    {
        count++;

        if (count == 1)
            CounterBtn.Text = $"Clicked {count} time";
        else
            CounterBtn.Text = $"Clicked {count} times";

        SemanticScreenReader.Announce(CounterBtn.Text);
    }

    // Add this method for Google Pay navigation
    private async void OnGooglePayClicked(object sender, EventArgs e)
    {
        try
        {
            // Simple navigation to PaymentPage
            var googlePayService = Handler?.MauiContext?.Services?.GetService<IGooglePayService>();

            var paymentPage = new TestApp.Pages.PaymentPage(googlePayService);
            await Navigation.PushAsync(paymentPage);
        }
        catch (Exception ex)
        { 
            await DisplayAlert("Error", $"Navigation failed: {ex.Message}", "OK");
        }
    }
}