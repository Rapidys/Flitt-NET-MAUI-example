using Microsoft.Maui.Controls;
using TestApp.Services;
using Newtonsoft.Json.Linq;
using TestApp.Pages;

namespace TestApp.Services
{
    public class FlittWebViewHandler
    {
        public async Task<PaymentResult> ShowWebViewAsync(PayConfirmation confirmation)
        {
            System.Diagnostics.Debug.WriteLine("=== FlittWebViewHandler.ShowWebViewAsync called ===");
            
            try
            {
                // Get the current PaymentPage instance
                var currentPage = GetCurrentPaymentPage();
                
                if (currentPage == null)
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: Could not find PaymentPage");
                    return new PaymentResult 
                    { 
                        Success = false, 
                        Error = "Could not find PaymentPage to show WebView" 
                    };
                }
                
                System.Diagnostics.Debug.WriteLine("Found PaymentPage, calling ShowWebViewAsync");
                
                // Call the PaymentPage's ShowWebViewAsync method
                var result = await currentPage.ShowWebViewAsync(confirmation);
                
                System.Diagnostics.Debug.WriteLine($"=== WebView completed in handler - Success: {result?.Success} ===");
                
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in FlittWebViewHandler: {ex.Message}");
                return new PaymentResult 
                { 
                    Success = false, 
                    Error = $"WebView handler error: {ex.Message}" 
                };
            }
        }
        
        private PaymentPage GetCurrentPaymentPage()
        {
            try
            {
                var mainPage = Application.Current?.MainPage;
                
                // Check if it's directly a PaymentPage
                if (mainPage is PaymentPage paymentPage)
                {
                    System.Diagnostics.Debug.WriteLine("Found PaymentPage as MainPage");
                    return paymentPage;
                }
                
                // Check if it's in a NavigationPage
                if (mainPage is NavigationPage navPage)
                {
                    var currentPage = navPage.CurrentPage;
                    if (currentPage is PaymentPage navPaymentPage)
                    {
                        System.Diagnostics.Debug.WriteLine("Found PaymentPage in NavigationPage");
                        return navPaymentPage;
                    }
                }
                
                // Check if it's in a Shell
                if (mainPage is Shell shell)
                {
                    var currentPage = shell.CurrentPage;
                    if (currentPage is PaymentPage shellPaymentPage)
                    {
                        System.Diagnostics.Debug.WriteLine("Found PaymentPage in Shell");
                        return shellPaymentPage;
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"Could not find PaymentPage. MainPage type: {mainPage?.GetType().Name}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error finding PaymentPage: {ex.Message}");
                return null;
            }
        }
    }

    // Keep existing supporting classes (no changes needed)
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