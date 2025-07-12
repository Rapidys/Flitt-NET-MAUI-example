#if ANDROID
using Android.App;
using Android.Content;
using Android.Gms.Tasks;
using Android.Gms.Wallet;
using AndroidX.AppCompat.App;
using Java.Lang;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using TestApp.Services;
using Exception = System.Exception;
namespace TestApp.Services
{
    public class GooglePayService : Java.Lang.Object, IOnCompleteListener
    {
        private const int GOOGLE_PAY_REQUEST_CODE = 999;
        
        private PaymentsClient _paymentsClient;
        private Activity _activity;
        private TaskCompletionSource<GooglePayResult> _paymentTaskCompletionSource;
        private FlittService _flittService;
        private FlittGooglePayCall _currentGooglePayCall;

        public GooglePayService(Activity activity, int merchantId)
        {
            _activity = activity;
            _flittService = new FlittService(merchantId);
            // Don't initialize Google Pay here - we'll do it with proper environment
        }

        private void InitializeGooglePay(string environment = "TEST")
        {
            var walletEnvironment = environment == "PRODUCTION" 
                ? WalletConstants.EnvironmentProduction 
                : WalletConstants.EnvironmentTest;

            var walletOptions = new WalletClass.WalletOptions.Builder()
                .SetEnvironment(walletEnvironment)
                .Build();

            _paymentsClient = WalletClass.GetPaymentsClient(_activity, walletOptions);
            
            System.Diagnostics.Debug.WriteLine($"Initialized Google Pay with environment: {environment}");
        }

        public async Task<bool> IsReadyToPayAsync()
        {
            try
            {
                // Initialize with test environment for readiness check
                if (_paymentsClient == null)
                {
                    InitializeGooglePay("TEST");
                }

                var request = GetIsReadyToPayRequest();
                var task = _paymentsClient.IsReadyToPay(request);
                
                var tcs = new TaskCompletionSource<bool>();
                task.AddOnCompleteListener(new OnCompleteListener<Java.Lang.Boolean>((result) =>
                {
                    if (result.IsSuccessful)
                    {
                        var boolResult = result.Result as Java.Lang.Boolean;
                        tcs.SetResult(boolResult?.BooleanValue() ?? false);
                    }
                    else
                    {
                        tcs.SetResult(false);
                    }
                }));
                
                return await tcs.Task;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IsReadyToPay Error: {ex.Message}");
                return false;
            }
        }

        // Proper token-based Google Pay initialization
        public async Task<GooglePayResult> InitializeGooglePayFromTokenAsync(string token)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Initializing Google Pay from token: {token}");
                
                // Step 1: Get Google Pay configuration from Flitt API
                var config = await _flittService.GetGooglePayConfigAsync(token);
                
                // Step 2: Parse environment from config
                var configData = JObject.Parse(config.GooglePayData);
                var environment = configData["environment"]?.ToString() ?? "TEST";
                
                System.Diagnostics.Debug.WriteLine($"Google Pay environment: {environment}");
                System.Diagnostics.Debug.WriteLine($"Google Pay config: {config.GooglePayData}");
                
                // Step 3: Initialize Google Pay with correct environment
                InitializeGooglePay(environment);
                
                // Step 4: Setup payment task
                _paymentTaskCompletionSource = new TaskCompletionSource<GooglePayResult>();
                
                // Store the call info for processing the result
                _currentGooglePayCall = new FlittGooglePayCall
                {
                    Token = token,
                    PaymentSystem = config.PaymentSystem,
                    CallbackUrl = config.CallbackUrl
                };

                // Step 5: Create PaymentDataRequest from Flitt config
                var request = PaymentDataRequest.FromJson(config.GooglePayData);
                
                // Step 6: Launch Google Pay
                var task = _paymentsClient.LoadPaymentData(request);
                AutoResolveHelper.ResolveTask(task, _activity, GOOGLE_PAY_REQUEST_CODE);
                
                return await _paymentTaskCompletionSource.Task;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"InitializeGooglePayFromToken Error: {ex.Message}");
                return new GooglePayResult { Success = false, Error = ex.Message };
            }
        }

        // // Flitt URL method (unchanged)
        // public async Task<GooglePayResult> LaunchFromFlittUrlAsync(string flittUrl)
        // {
        //     try
        //     {
        //         var uri = Android.Net.Uri.Parse(flittUrl);
        //         var jsonEncoded = uri.EncodedFragment;
        //         
        //         if (!string.IsNullOrEmpty(jsonEncoded) && jsonEncoded.StartsWith("__WA__="))
        //         {
        //             jsonEncoded = jsonEncoded.Substring("__WA__=".Length);
        //         }
        //         else
        //         {
        //             return new GooglePayResult { Success = false, Error = "Payment fragment not found in URL" };
        //         }
        //
        //         if (string.IsNullOrWhiteSpace(jsonEncoded))
        //         {
        //             return new GooglePayResult { Success = false, Error = "Flitt URL does not contain payment data" };
        //         }
        //
        //         var decodedJson = Java.Net.URLDecoder.Decode(jsonEncoded, "UTF-8");
        //         
        //         // Parse environment from Flitt data
        //         var flittData = JObject.Parse(decodedJson);
        //         var args = flittData["args"] as JObject;
        //         var environment = args?["environment"]?.ToString() ?? "TEST";
        //         
        //         // Initialize Google Pay with correct environment
        //         InitializeGooglePay(environment);
        //         
        //         var request = PaymentDataRequest.FromJson(decodedJson);
        //         
        //         _paymentTaskCompletionSource = new TaskCompletionSource<GooglePayResult>();
        //         _currentGooglePayCall = null; // Direct JSON processing
        //         
        //         var task = _paymentsClient.LoadPaymentData(request);
        //         AutoResolveHelper.ResolveTask(task, _activity, GOOGLE_PAY_REQUEST_CODE);
        //         
        //         return await _paymentTaskCompletionSource.Task;
        //     }
        //     catch (Exception ex)
        //     {
        //         System.Diagnostics.Debug.WriteLine($"LaunchFromFlittUrl Error: {ex.Message}");
        //         return new GooglePayResult { Success = false, Error = ex.Message };
        //     }
        // }

        private IsReadyToPayRequest GetIsReadyToPayRequest()
        {
            return IsReadyToPayRequest.NewBuilder()
                .AddAllowedPaymentMethod(WalletConstants.PaymentMethodCard)
                .AddAllowedPaymentMethod(WalletConstants.PaymentMethodTokenizedCard)
                .Build();
        }

        public void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            if (requestCode == GOOGLE_PAY_REQUEST_CODE)
            {
                if (resultCode == Result.Ok)
                {
                    var paymentData = PaymentData.GetFromIntent(data);
                    var paymentToken = paymentData.ToJson();
                    
                    System.Diagnostics.Debug.WriteLine($"Google Pay Success - Payment Data: {paymentToken}");
                    
                    // If we have a current Google Pay call, process it through Flitt
                    if (_currentGooglePayCall != null)
                    {
                        ProcessGooglePayThroughFlitt(_currentGooglePayCall, paymentToken)
                            .ContinueWith(task =>
                            {
                                if (task.IsCompletedSuccessfully)
                                {
                                    _paymentTaskCompletionSource?.SetResult(task.Result);
                                }
                                else
                                {
                                    var error = task.Exception?.GetBaseException().Message ?? "Unknown error";
                                    System.Diagnostics.Debug.WriteLine($"Flitt processing error: {error}");
                                    _paymentTaskCompletionSource?.SetResult(new GooglePayResult 
                                    { 
                                        Success = false, 
                                        Error = error
                                    });
                                }
                                _currentGooglePayCall = null;
                            });
                    }
                    else
                    {
                        // Direct payment without Flitt processing
                        _paymentTaskCompletionSource?.SetResult(new GooglePayResult 
                        { 
                            Success = true, 
                            PaymentData = paymentToken 
                        });
                    }
                }
                else if (resultCode == Result.Canceled)
                {
                    System.Diagnostics.Debug.WriteLine("Google Pay Cancelled by user");
                    _paymentTaskCompletionSource?.SetResult(new GooglePayResult 
                    { 
                        Success = false, 
                        Error = "Payment cancelled by user" 
                    });
                }
                else
                {
                    var status = AutoResolveHelper.GetStatusFromIntent(data);
                    var error = $"Payment failed: {status?.StatusMessage}";
                    System.Diagnostics.Debug.WriteLine($"Google Pay Error: {error}");
                    _paymentTaskCompletionSource?.SetResult(new GooglePayResult 
                    { 
                        Success = false, 
                        Error = error
                    });
                }
            }
        }

        private async Task<GooglePayResult> ProcessGooglePayThroughFlitt(FlittGooglePayCall googlePayCall, string paymentData)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Processing payment through Flitt - Token: {googlePayCall.Token}, PaymentSystem: {googlePayCall.PaymentSystem}");
                
                var receipt = await _flittService.ProcessGooglePaymentAsync(
                    googlePayCall.Token, 
                    googlePayCall.PaymentSystem, 
                    paymentData);

                System.Diagnostics.Debug.WriteLine($"Flitt processing successful - Receipt: {receipt.PaymentId}");

                return new GooglePayResult
                {
                    Success = true,
                    PaymentData = paymentData,
                    Receipt = receipt
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Flitt processing failed: {ex.Message}");
                return new GooglePayResult { Success = false, Error = ex.Message };
            }
        }

        public void OnComplete(Android.Gms.Tasks.Task task)
        {
            if (task.IsSuccessful)
            {
                System.Diagnostics.Debug.WriteLine("Google Pay task completed successfully");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Google Pay task failed: {task.Exception?.Message}");
            }
        }
    }

    // Helper class for OnCompleteListener
    public class OnCompleteListener<T> : Java.Lang.Object, IOnCompleteListener where T : Java.Lang.Object
    {
        private readonly Action<Android.Gms.Tasks.Task> _onComplete;

        public OnCompleteListener(Action<Android.Gms.Tasks.Task> onComplete)
        {
            _onComplete = onComplete;
        }

        public void OnComplete(Android.Gms.Tasks.Task task)
        {
            _onComplete?.Invoke(task);
        }
    }
}
#endif