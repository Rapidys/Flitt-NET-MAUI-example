using System;
using Microsoft.Maui.Controls;
using TestApp.Services;

namespace TestApp.Pages;

public partial class PaymentPage : ContentPage
{
    private readonly IGooglePayService _googlePayService;

    public PaymentPage(IGooglePayService googlePayService)
    {
        InitializeComponent();
        _googlePayService = googlePayService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        if (_googlePayService != null)
        {
            var isReady = await _googlePayService.IsReadyToPayAsync();
            ResultLabel.Text = isReady ? "Google Pay is ready!" : "Google Pay not available";
        }
        else
        {
            ResultLabel.Text = "Google Pay service not available on this platform";
        }
    }

    private async void OnGooglePayClicked(object sender, EventArgs e)
    {
        try
        {
            LoadingIndicator.IsVisible = true;
            GooglePayButton.IsEnabled = false;
            ResultLabel.Text = "Processing payment...";
            
            var result = await _googlePayService.InitializeGooglePayFromTokenAsync("058e8ccae486b28266461ea6cf192b65b45120e6");
            
            if (result.Success)
            {
                ResultLabel.Text = "Token-based payment successful!";
                
                if (result.Receipt != null)
                {
                    var receiptInfo = $"Payment ID: {result.Receipt.PaymentId}\n" +
                                      $"Amount: {result.Receipt.Amount} {result.Receipt.Currency}\n" +
                                      $"Status: {result.Receipt.OrderStatus}\n" +
                                      $"Card: {result.Receipt.MaskedCard}\n" +
                                      $"RRN: {result.Receipt.RRN}\n\n" +
                                      $"Raw JSON:\n{result.PaymentData}";
                    
                    await DisplayAlert("Payment Successful", receiptInfo, "OK");
                }
                else
                {
                    var jsonResponse = result.PaymentData ?? "No payment data received";
                    await DisplayAlert("Success", $"Payment completed!\n\nJSON Response:\n{jsonResponse}", "OK");
                }
            }
            else
            {
                ResultLabel.Text = $"Token-based payment failed: {result.Error}";
                await DisplayAlert("Error", result.Error, "OK");
            }
        }
        catch (Exception ex)
        {
            ResultLabel.Text = $"Error: {ex.Message}";
            await DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            GooglePayButton.IsEnabled = true;
        }
    }

}