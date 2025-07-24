using System;
using Microsoft.Maui.Controls;
using TestApp.Services;
using Newtonsoft.Json.Linq;

#if ANDROID
using Android.Webkit;
using Microsoft.Maui.Handlers;
using AWebView = Android.Webkit.WebView;
#endif

namespace TestApp.Pages;

public partial class PaymentPage : ContentPage
{
    private readonly IGooglePayService _googlePayService;
    private TaskCompletionSource<PaymentResult> _webViewCompletion;
    private PayConfirmation _currentConfirmation;

    // WebView callback constants - EXACTLY like Android SDK
    private const string URL_START_PATTERN = "http://secure-redirect.flitt.com/submit/#";

    public PaymentPage(IGooglePayService googlePayService)
    {
        InitializeComponent();
        _googlePayService = googlePayService;

        // Configure WebView settings when the page loads
        Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object sender, EventArgs e)
    {
        ConfigureWebView();
    }

    private void ConfigureWebView()
    {
#if ANDROID
        try
        {
            // Configure WebView settings for Android (like React Native)
            var handler = PaymentWebView.Handler as WebViewHandler;
            if (handler?.PlatformView is AWebView webView)
            {
                var settings = webView.Settings;

                // Enable JavaScript (critical for 3DS)
                settings.JavaScriptEnabled = true;

                // Enable DOM storage (like React Native domStorageEnabled)
                settings.DomStorageEnabled = true;

                // Set cache mode
                settings.CacheMode = CacheModes.NoCache;

                // Enable mixed content (HTTP/HTTPS)
                settings.MixedContentMode = MixedContentHandling.CompatibilityMode;

                // Set user agent to avoid mobile detection issues
                settings.UserAgentString = settings.UserAgentString + " FlittPayment/1.0";

                // Enable viewport meta tag support
                settings.UseWideViewPort = true;
                settings.LoadWithOverviewMode = true;

                // Enable zooming but set initial scale
                settings.BuiltInZoomControls = false;
                settings.DisplayZoomControls = false;

                // Allow file access (needed for some 3DS implementations)
                settings.AllowFileAccess = true;
                settings.AllowContentAccess = true;

                System.Diagnostics.Debug.WriteLine("✅ WebView settings configured successfully");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Could not access WebView platform view");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error configuring WebView: {ex.Message}");
        }
#endif
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

            System.Diagnostics.Debug.WriteLine("=== STARTING PAYMENT PROCESS ===");

            var startTime = DateTime.Now;
            System.Diagnostics.Debug.WriteLine($"Payment started at: {startTime}");

            var order = Order.CreateTestOrder();

            var result = await _googlePayService.InitializeGooglePayAsync(order);
            // var result = await _googlePayService.InitializeGooglePayFromTokenAsync("97397049fc4d4fc70311a1f186359cdc70325941");
            

            var endTime = DateTime.Now;
            var duration = endTime - startTime;
            System.Diagnostics.Debug.WriteLine($"Payment completed at: {endTime}");
            System.Diagnostics.Debug.WriteLine($"Total duration: {duration.TotalSeconds} seconds");

            System.Diagnostics.Debug.WriteLine("=== PAYMENT PROCESS COMPLETED ===");
            System.Diagnostics.Debug.WriteLine($"Result is null: {result == null}");
            if (result != null)
            {
                System.Diagnostics.Debug.WriteLine($"Success: {result.Success}");
                System.Diagnostics.Debug.WriteLine($"Error: {result.Error}");
                System.Diagnostics.Debug.WriteLine($"PaymentData: {result.PaymentData}");
                System.Diagnostics.Debug.WriteLine($"Receipt is null: {result.Receipt == null}");
            }

            if (result?.Success == true)
            {
                ResultLabel.Text = "Payment successful!";

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
                var errorMsg = result?.Error ?? "Unknown error occurred";
                ResultLabel.Text = $"Payment failed: {errorMsg}";
                await DisplayAlert("Error", errorMsg, "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"EXCEPTION in OnGooglePayClicked: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            ResultLabel.Text = $"Error: {ex.Message}";
            await DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            GooglePayButton.IsEnabled = true;
        }
    }

    public async Task<PaymentResult> ShowWebViewAsync(PayConfirmation confirmation)
    {
        var methodStartTime = DateTime.Now;
        System.Diagnostics.Debug.WriteLine($"=== ShowWebViewAsync started at {methodStartTime} ===");

        try
        {
            _webViewCompletion = new TaskCompletionSource<PaymentResult>();
            _currentConfirmation = confirmation;

            var tcsId = _webViewCompletion.GetHashCode();
            System.Diagnostics.Debug.WriteLine($"Created TaskCompletionSource with ID: {tcsId}");

            await Dispatcher.DispatchAsync(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("Loading WebView content with enhanced settings");
                    System.Diagnostics.Debug.WriteLine($"HTML Length: {confirmation.HtmlPageContent?.Length}");
                    System.Diagnostics.Debug.WriteLine($"Base URL: {confirmation.Url}");

                    // Enhance the HTML with viewport meta tag (like React Native)
                    var enhancedHtml = AddViewportMeta(confirmation.HtmlPageContent);

                    var htmlSource = new HtmlWebViewSource
                    {
                        Html = enhancedHtml,
                        BaseUrl = confirmation.Url
                    };

                    PaymentWebView.Source = htmlSource;

                    // Hide payment UI and show WebView
                    PaymentUI.IsVisible = false;
                    WebViewOverlay.IsVisible = true;

                    // Reconfigure WebView settings after loading new content
                    Task.Delay(100).ContinueWith(_ => ConfigureWebView());

                    System.Diagnostics.Debug.WriteLine("WebView content loaded with enhanced settings");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in UI dispatch: {ex.Message}");
                    _webViewCompletion?.SetResult(new PaymentResult
                    {
                        Success = false,
                        Error = $"Failed to load authentication page: {ex.Message}"
                    });
                }
            });

            System.Diagnostics.Debug.WriteLine($"About to await TaskCompletionSource {tcsId}...");

            // Timeout for testing
            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(3));
            var completedTask = await Task.WhenAny(_webViewCompletion.Task, timeoutTask);

            var waitEndTime = DateTime.Now;
            var waitDuration = waitEndTime - methodStartTime;
            System.Diagnostics.Debug.WriteLine($"Wait completed after {waitDuration.TotalSeconds} seconds");

            if (completedTask == timeoutTask)
            {
                System.Diagnostics.Debug.WriteLine("⏰ TIMEOUT: WebView timed out after 3 minutes");
                return new PaymentResult
                {
                    Success = false,
                    Error = "WebView authentication timed out"
                };
            }

            var result = await _webViewCompletion.Task;
            System.Diagnostics.Debug.WriteLine(
                $"✅ TaskCompletionSource {tcsId} completed with result: Success={result?.Success}, Error={result?.Error}");
            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Exception in ShowWebViewAsync: {ex.Message}");
            return new PaymentResult
            {
                Success = false,
                Error = $"ShowWebViewAsync error: {ex.Message}"
            };
        }
    }

    // Add viewport meta tag like React Native does
    private string AddViewportMeta(string html)
    {
        if (string.IsNullOrEmpty(html))
            return html;

        // Check if viewport meta tag already exists
        if (html.Contains("name=\"viewport\""))
            return html;

        // Add viewport meta tag to head
        var viewportMeta =
            "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0, user-scalable=no\">";

        if (html.Contains("<head>"))
        {
            return html.Replace("<head>", $"<head>{viewportMeta}");
        }
        else if (html.Contains("<html>"))
        {
            return html.Replace("<html>", $"<html><head>{viewportMeta}</head>");
        }
        else
        {
            return $"<html><head>{viewportMeta}</head><body>{html}</body></html>";
        }
    }

    private void OnWebViewNavigating(object sender, WebNavigatingEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"🔄 NAVIGATING to: {e.Url}");

        if (CheckUrl(e.Url))
        {
            System.Diagnostics.Debug.WriteLine("🚫 CALLBACK DETECTED - CANCELING NAVIGATION");
            e.Cancel = true;
        }
    }

    private void OnWebViewNavigated(object sender, WebNavigatedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"✅ NAVIGATED to: {e.Url} (Result: {e.Result})");

        // Always check URL on navigation, regardless of result
        CheckUrl(e.Url);
    }

    private bool CheckUrl(string url)
    {
        if (string.IsNullOrEmpty(url) || _currentConfirmation == null)
        {
            return false;
        }

        var tcsId = _webViewCompletion?.GetHashCode();
        System.Diagnostics.Debug.WriteLine($"🔍 CheckUrl called for TCS {tcsId}: {url}");

        bool detectsStartPattern = url.StartsWith(URL_START_PATTERN);
        bool detectsApiToken = url.StartsWith(_currentConfirmation.Host + "/api/checkout?token=");
        bool detectsCallbackUrl = false;

        try
        {
            var incoming = new Uri(url);
            var cb = new Uri(_currentConfirmation.CallbackUrl);
            detectsCallbackUrl = incoming.Scheme.Equals(cb.Scheme, StringComparison.OrdinalIgnoreCase) &&
                                 incoming.Host.Equals(cb.Host, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"URI parsing error: {ex.Message}");
        }

        System.Diagnostics.Debug.WriteLine(
            $"Pattern checks - Start: {detectsStartPattern}, API: {detectsApiToken}, Callback: {detectsCallbackUrl}");

        if (detectsStartPattern || detectsCallbackUrl || detectsApiToken)
        {
            System.Diagnostics.Debug.WriteLine($"🎯 CALLBACK DETECTED for TCS {tcsId}!");

            JObject response = null;

            if (detectsStartPattern)
            {
                string jsonPart = url.Substring(URL_START_PATTERN.Length);
                System.Diagnostics.Debug.WriteLine($"📝 Extracting JSON: {jsonPart}");

                try
                {
                    response = JObject.Parse(jsonPart);
                    System.Diagnostics.Debug.WriteLine("✅ Parsed JSON successfully");
                }
                catch (Exception)
                {
                    try
                    {
                        string decoded = Uri.UnescapeDataString(jsonPart);
                        response = JObject.Parse(decoded);
                        System.Diagnostics.Debug.WriteLine("✅ Parsed decoded JSON successfully");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ JSON parsing failed: {ex.Message}");
                    }
                }
            }

            var result = new PaymentResult { Success = true, Response = response };
            CompleteWebView(result);
            return true;
        }

        return false;
    }

    private void CompleteWebView(PaymentResult result)
    {
        var tcsId = _webViewCompletion?.GetHashCode();
        var timestamp = DateTime.Now;

        System.Diagnostics.Debug.WriteLine($"🏁 CompleteWebView called at {timestamp} for TCS {tcsId}");
        System.Diagnostics.Debug.WriteLine($"   Result: Success={result.Success}, Error={result.Error}");
        System.Diagnostics.Debug.WriteLine($"   TCS exists: {_webViewCompletion != null}");
        System.Diagnostics.Debug.WriteLine($"   TCS completed: {_webViewCompletion?.Task.IsCompleted}");

        try
        {
            // Hide WebView immediately
            Dispatcher.Dispatch(() =>
            {
                WebViewOverlay.IsVisible = false;
                PaymentUI.IsVisible = true;
                System.Diagnostics.Debug.WriteLine("🎭 UI updated - WebView hidden");
            });

            // Set the result
            if (_webViewCompletion != null && !_webViewCompletion.Task.IsCompleted)
            {
                System.Diagnostics.Debug.WriteLine($"📤 Setting result for TCS {tcsId}");
                _webViewCompletion.SetResult(result);
                System.Diagnostics.Debug.WriteLine($"✅ Result set successfully for TCS {tcsId}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(
                    $"⚠️ Cannot set result - TCS null: {_webViewCompletion == null}, completed: {_webViewCompletion?.Task.IsCompleted}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ CompleteWebView error: {ex.Message}");
        }
    }

    private async void OnCancelWebViewClicked(object sender, EventArgs e)
    {
        var result = await DisplayAlert("Cancel Authentication",
            "Are you sure you want to cancel the bank authentication?",
            "Yes, Cancel", "No, Continue");

        if (result)
        {
            CompleteWebView(new PaymentResult
            {
                Success = false,
                Error = "Authentication cancelled by user"
            });
        }
    }

    protected override bool OnBackButtonPressed()
    {
        if (WebViewOverlay.IsVisible)
        {
            Dispatcher.Dispatch(async () =>
            {
                var result = await DisplayAlert("Cancel Authentication",
                    "Are you sure you want to cancel the bank authentication?",
                    "Yes, Cancel", "No, Continue");

                if (result)
                {
                    CompleteWebView(new PaymentResult
                    {
                        Success = false,
                        Error = "Authentication cancelled by user"
                    });
                }
            });

            return true;
        }

        return base.OnBackButtonPressed();
    }
}