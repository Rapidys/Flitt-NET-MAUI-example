// Location: TestApp/Services/FlittWebViewHandler.cs (Simplified)
using Microsoft.Maui.Controls;
using TestApp.Services;
using Newtonsoft.Json.Linq;
using TestApp.Pages;

namespace TestApp.Services
{
    public class FlittWebViewHandler
    {
        private const string URL_START_PATTERN = "http://secure-redirect.flitt.com/submit/#";
        
        public async Task<PaymentResult> ShowWebViewAsync(PayConfirmation confirmation)
        {
            var tcs = new TaskCompletionSource<PaymentResult>();

            // Show WebView on the UI thread using MAUI's simple approach
            await Microsoft.Maui.Controls.Application.Current.Dispatcher.DispatchAsync(async () =>
            {
                try
                {
                    // Create and show the simple WebView page
                    var webViewPage = new SimpleWebView3DSPage(confirmation, tcs);
                    
                    // Navigate to the WebView page
                    var mainPage = Application.Current.MainPage;
                    if (mainPage is NavigationPage navPage)
                    {
                        await navPage.PushAsync(webViewPage);
                    }
                    else if (mainPage is Shell shell)
                    {
                        await shell.Navigation.PushAsync(webViewPage);
                    }
                    else
                    {
                        await mainPage.Navigation.PushAsync(webViewPage);
                    }
                }
                catch (System.Exception ex)
                {
                    tcs.SetResult(new PaymentResult 
                    { 
                        Success = false, 
                        Error = $"Failed to show WebView: {ex.Message}" 
                    });
                }
            });

            return await tcs.Task;
        }
    }

    // Keep existing supporting classes
    public class PayConfirmation
    {
        public string HtmlPageContent { get; set; }
        public string ContentType { get; set; }
        public string Url { get; set; }
        public string CallbackUrl { get; set; }
        public string Host { get; set; }
        public string Cookie { get; set; }
    }

    public class PaymentResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public JObject Response { get; set; }
    }
}