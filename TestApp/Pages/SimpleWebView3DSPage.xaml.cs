using Microsoft.Maui.Controls;
using TestApp.Services;
using Newtonsoft.Json.Linq;

namespace TestApp.Pages;

public partial class SimpleWebView3DSPage : ContentPage
{
    private const string URL_START_PATTERN = "http://secure-redirect.flitt.com/submit/#";
    
    private readonly PayConfirmation _confirmation;
    private readonly TaskCompletionSource<PaymentResult> _completionSource;

    public SimpleWebView3DSPage(PayConfirmation confirmation, TaskCompletionSource<PaymentResult> completionSource)
    {
        InitializeComponent();
        _confirmation = confirmation;
        _completionSource = completionSource;
        
        LoadWebViewContent();
    }

    private void LoadWebViewContent()
    {
        try
        {
            // Use MAUI's simple HtmlWebViewSource
            var htmlSource = new HtmlWebViewSource
            {
                Html = _confirmation.HtmlPageContent,
                BaseUrl = _confirmation.Url
            };

            PaymentWebView.Source = htmlSource;
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebView loading error: {ex.Message}");
            CompletePayment(new PaymentResult 
            { 
                Success = false, 
                Error = $"Failed to load authentication page: {ex.Message}" 
            });
        }
    }

    private void OnWebViewNavigating(object sender, WebNavigatingEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"WebView navigating to: {e.Url}");
        
        if (IsCallbackUrl(e.Url))
        {
            System.Diagnostics.Debug.WriteLine("Callback URL detected - canceling navigation");
            e.Cancel = true; // Stop the navigation
            HandleCallback(e.Url);
        }
    }

    private void OnWebViewNavigated(object sender, WebNavigatedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"WebView navigated to: {e.Url}");
        
        if (e.Result != WebNavigationResult.Success)
        {
            System.Diagnostics.Debug.WriteLine($"Navigation failed: {e.Result}");
            CompletePayment(new PaymentResult 
            { 
                Success = false, 
                Error = $"Navigation failed: {e.Result}" 
            });
        }
        else if (IsCallbackUrl(e.Url))
        {
            HandleCallback(e.Url);
        }
    }

    private bool IsCallbackUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        
        System.Diagnostics.Debug.WriteLine($"Checking if callback URL: {url}");
        
        // Check for the specific callback patterns
        if (url.StartsWith(URL_START_PATTERN))
        {
            System.Diagnostics.Debug.WriteLine("Matches URL_START_PATTERN");
            return true;
        }
        
        if (url.StartsWith(_confirmation.Host + "/api/checkout?token="))
        {
            System.Diagnostics.Debug.WriteLine("Matches API token pattern");
            return true;
        }
        
        // Check if URL matches callback URL pattern
        try
        {
            var uri = new Uri(url);
            var callbackUri = new Uri(_confirmation.CallbackUrl);
            var isCallback = uri.Scheme.Equals(callbackUri.Scheme, StringComparison.OrdinalIgnoreCase) &&
                           uri.Host.Equals(callbackUri.Host, StringComparison.OrdinalIgnoreCase);
            
            if (isCallback)
            {
                System.Diagnostics.Debug.WriteLine("Matches callback URL pattern");
            }
            
            return isCallback;
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking callback URL: {ex.Message}");
            return false;
        }
    }

    private void HandleCallback(string url)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"Handling callback URL: {url}");
            
            JObject response = null;
            
            if (url.StartsWith(URL_START_PATTERN))
            {
                var jsonPart = url.Substring(URL_START_PATTERN.Length);
                System.Diagnostics.Debug.WriteLine($"Extracting JSON from URL: {jsonPart}");
                
                try
                {
                    response = JObject.Parse(jsonPart);
                    System.Diagnostics.Debug.WriteLine($"Successfully parsed JSON from URL: {response}");
                }
                catch (System.Exception)
                {
                    try
                    {
                        var decoded = Uri.UnescapeDataString(jsonPart);
                        response = JObject.Parse(decoded);
                        System.Diagnostics.Debug.WriteLine("Successfully parsed decoded JSON from URL");
                    }
                    catch (System.Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to parse JSON: {ex.Message}");
                    }
                }
            }

            ProcessPaymentResult(response);
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Callback handling error: {ex.Message}");
            CompletePayment(new PaymentResult 
            { 
                Success = false, 
                Error = $"Failed to process authentication result: {ex.Message}" 
            });
        }
    }

    private void ProcessPaymentResult(JObject response)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Processing payment result");
            
            if (response == null)
            {
                System.Diagnostics.Debug.WriteLine("No response data - assuming success");
                CompletePayment(new PaymentResult { Success = true });
                return;
            }

            System.Diagnostics.Debug.WriteLine($"Response data: {response.ToString()}");

            // Check if URL is valid
            var url = response["url"]?.ToString();
            if (!string.IsNullOrEmpty(url) && !url.StartsWith(_confirmation.CallbackUrl))
            {
                System.Diagnostics.Debug.WriteLine($"Invalid callback URL: {url}");
                CompletePayment(new PaymentResult 
                { 
                    Success = false, 
                    Error = "Invalid authentication response" 
                });
                return;
            }

            // Check order data if present
            var orderData = response["params"] as JObject;
            if (orderData != null)
            {
                var responseStatus = orderData["response_status"]?.ToString();
                System.Diagnostics.Debug.WriteLine($"Response status: {responseStatus}");
                
                if (responseStatus != "success")
                {
                    var errorMessage = orderData["error_message"]?.ToString() ?? "Authentication failed";
                    System.Diagnostics.Debug.WriteLine($"Authentication failed: {errorMessage}");
                    CompletePayment(new PaymentResult 
                    { 
                        Success = false, 
                        Error = errorMessage 
                    });
                    return;
                }
            }

            System.Diagnostics.Debug.WriteLine("Authentication completed successfully");
            CompletePayment(new PaymentResult { Success = true, Response = response });
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Payment result processing error: {ex.Message}");
            CompletePayment(new PaymentResult 
            { 
                Success = false, 
                Error = $"Failed to process authentication: {ex.Message}" 
            });
        }
    }

    private async void CompletePayment(PaymentResult result)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"Completing payment - Success: {result.Success}, Error: {result.Error}");
            
            // Set the result
            _completionSource?.SetResult(result);
            
            // Navigate back on UI thread
            await Dispatcher.DispatchAsync(async () =>
            {
                try
                {
                    if (Navigation.NavigationStack.Count > 1)
                    {
                        await Navigation.PopAsync();
                    }
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}");
                }
            });
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CompletePayment error: {ex.Message}");
        }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        var result = await DisplayAlert("Cancel Authentication", 
            "Are you sure you want to cancel the bank authentication?", 
            "Yes, Cancel", "No, Continue");
        
        if (result)
        {
            CompletePayment(new PaymentResult 
            { 
                Success = false, 
                Error = "Authentication cancelled by user" 
            });
        }
    }

    protected override bool OnBackButtonPressed()
    {
        // Handle hardware back button
        Dispatcher.Dispatch(async () =>
        {
            var result = await DisplayAlert("Cancel Authentication", 
                "Are you sure you want to cancel the bank authentication?", 
                "Yes, Cancel", "No, Continue");
            
            if (result)
            {
                CompletePayment(new PaymentResult 
                { 
                    Success = false, 
                    Error = "Authentication cancelled by user" 
                });
            }
        });
        
        return true; // Prevent default back button behavior
    }
}