#if ANDROID
using Android.Content;
using Java.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace TestApp.Services
{
    public class FlittService
    {
        private readonly int _merchantId;
        private readonly string _apiHost;
        private readonly HttpClient _httpClient;

        public FlittService(int merchantId, string apiHost = "https://sandbox.pay.flitt.dev")
        {
            _merchantId = merchantId;
            _apiHost = apiHost;
            _httpClient = new HttpClient();
        }

        public async Task<FlittGooglePayConfig> GetGooglePayConfigAsync(string token)
        {
            try
            {
                var request = new Dictionary<string, object>
                {
                    ["merchant_id"] = _merchantId,
                    ["token"] = token
                };

                var response = await CallApiAsync("/api/checkout/ajax/mobile_pay", request);
                
                if (response.ContainsKey("error_message"))
                {
                    throw new Exception($"API Error: {response["error_message"]}");
                }

                var paymentSystem = response["payment_system"].ToString();
                var methods = JArray.Parse(response["methods"].ToString());

                JObject googlePayData = null;
                foreach (var method in methods)
                {
                    var methodObj = method as JObject;
                    if (methodObj?["supportedMethods"]?.ToString() == "https://google.com/pay")
                    {
                        googlePayData = methodObj["data"] as JObject;
                        break;
                    }
                }

                if (googlePayData == null)
                {
                    throw new Exception("Google Pay not supported for this merchant");
                }

                return new FlittGooglePayConfig
                {
                    PaymentSystem = paymentSystem,
                    GooglePayData = googlePayData.ToString(),
                    Token = token,
                    CallbackUrl = "https://callback"
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get Google Pay config: {ex.Message}", ex);
            }
        }

        public async Task<Receipt> ProcessGooglePaymentAsync(string token, string paymentSystem, string paymentData, string email = null)
        {
            try
            {
                var request = new Dictionary<string, object>
                {
                    ["payment_system"] = paymentSystem,
                    ["token"] = token,
                    ["data"] = JObject.Parse(paymentData)
                };

                if (!string.IsNullOrEmpty(email))
                {
                    request["email"] = email;
                }

                var checkoutResponse = await CallApiAsync("/api/checkout/ajax", request);
                
                var url = checkoutResponse["url"].ToString();
                var callbackUrl = "https://callback";

                if (url.StartsWith(callbackUrl))
                {
                    // No 3DS required, get the order directly
                    return await GetOrderAsync(token);
                }
                else
                {
                    // 3DS required - OPEN WEBVIEW HERE using FlittWebViewHandler
                    System.Diagnostics.Debug.WriteLine("3DS authentication required - opening WebView with FlittWebViewHandler");
                    
                    // Get the 3DS form data
                    var sendData = JObject.Parse(checkoutResponse["send_data"].ToString());
                    
                    // Prepare form data for 3DS POST
                    var formData = $"MD={URLEncoder.Encode(sendData["MD"].ToString(), "UTF-8")}&" +
                                  $"PaReq={URLEncoder.Encode(sendData["PaReq"].ToString(), "UTF-8")}&" +
                                  $"TermUrl={URLEncoder.Encode(sendData["TermUrl"].ToString(), "UTF-8")}";

                    System.Diagnostics.Debug.WriteLine($"3DS Form Data: {formData}");
                    System.Diagnostics.Debug.WriteLine($"3DS URL: {url}");

                    // Make HTTP request to get 3DS authentication page HTML
                    var content = new StringContent(formData, Encoding.UTF8, "application/x-www-form-urlencoded");
                    var response = await _httpClient.PostAsync(url, content);
                    var htmlContent = await response.Content.ReadAsStringAsync();
                    var contentType = response.Content.Headers.ContentType?.ToString() ?? "text/html";

                    System.Diagnostics.Debug.WriteLine($"Got 3DS HTML content (length: {htmlContent.Length})");

                    // Create PayConfirmation object for WebView
                    var confirmation = new PayConfirmation
                    {
                        HtmlPageContent = htmlContent,
                        ContentType = contentType,
                        Url = url,
                        CallbackUrl = callbackUrl,
                        Host = _apiHost,
                        Cookie = ExtractCookieFromResponse(response)
                    };

                    // USE FlittWebViewHandler TO SHOW THE WEBVIEW FOR 3DS AUTHENTICATION
                    System.Diagnostics.Debug.WriteLine("Creating FlittWebViewHandler and showing WebView");
                    var webViewHandler = new FlittWebViewHandler();
                    var webViewResult = await webViewHandler.ShowWebViewAsync(confirmation);

                    System.Diagnostics.Debug.WriteLine($"MAUI LOG: {webViewResult}");

                    if (webViewResult.Success)
                    {
                        System.Diagnostics.Debug.WriteLine("3DS authentication completed successfully");
                        
                        if (webViewResult.Response != null)
                        {
                            // Parse order data from 3DS response
                            var orderData = webViewResult.Response["params"] as JObject;
                            if (orderData != null)
                            {
                                System.Diagnostics.Debug.WriteLine("Parsing order data from 3DS response");
                                return ParseOrderFromResponse(orderData, null);
                            }
                        }
                        
                        // If no order data in response, fetch it from the API
                        System.Diagnostics.Debug.WriteLine("No order data in 3DS response, fetching from API");
                        return await GetOrderAsync(token);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"3DS authentication failed: {webViewResult.Error}");
                        throw new Exception(webViewResult.Error ?? "3DS authentication failed");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to process Google Pay payment: {ex.Message}", ex);
            }
        }

        private string ExtractCookieFromResponse(HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                return string.Join("; ", cookies);
            }
            return null;
        }
        private Receipt ParseOrderFromResponse(JObject orderData, string responseUrl)
        {
            return new Receipt
            {
                MaskedCard = orderData["masked_card"]?.ToString(),
                CardBin = orderData["card_bin"]?.ToString(),
                Amount = orderData["amount"]?.ToObject<int>() ?? 0,
                PaymentId = orderData["payment_id"]?.ToObject<int>() ?? 0,
                Currency = orderData["currency"]?.ToString(),
                OrderStatus = orderData["order_status"]?.ToString(),
                TransactionType = orderData["tran_type"]?.ToString(),
                RRN = orderData["rrn"]?.ToString(),
                ApprovalCode = orderData["approval_code"]?.ToString(),
                ResponseCode = orderData["response_code"]?.ToString(),
                PaymentSystem = orderData["payment_system"]?.ToString(),
                ResponseUrl = responseUrl
            };
        }

        
        public async Task<Receipt> GetOrderAsync(string token)
        {
            try
            {
                var request = new Dictionary<string, object>
                {
                    ["token"] = token
                };

                var response = await CallApiAsync("/api/checkout/merchant/order", request);
                var orderData = JObject.Parse(response["order_data"].ToString());
                var responseUrl = response["response_url"].ToString();

                return new Receipt
                {
                    MaskedCard = orderData["masked_card"]?.ToString(),
                    CardBin = orderData["card_bin"]?.ToString(),
                    Amount = orderData["amount"]?.ToObject<int>() ?? 0,
                    PaymentId = orderData["payment_id"]?.ToObject<int>() ?? 0,
                    Currency = orderData["currency"]?.ToString(),
                    OrderStatus = orderData["order_status"]?.ToString(),
                    TransactionType = orderData["tran_type"]?.ToString(),
                    RRN = orderData["rrn"]?.ToString(),
                    ApprovalCode = orderData["approval_code"]?.ToString(),
                    ResponseCode = orderData["response_code"]?.ToString(),
                    PaymentSystem = orderData["payment_system"]?.ToString(),
                    ResponseUrl = responseUrl
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get order: {ex.Message}", ex);
            }
        }

        private async Task<Dictionary<string, object>> CallApiAsync(string path, Dictionary<string, object> requestData)
        {
            try
            {
                var requestBody = new
                {
                    request = requestData
                };

                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiHost}{path}", content);
                var responseString = await response.Content.ReadAsStringAsync();

                var responseObj = JObject.Parse(responseString);
                var responseData = responseObj["response"] as JObject;

                if (responseData == null)
                {
                    throw new Exception("Invalid API response format");
                }

                var result = responseData.ToObject<Dictionary<string, object>>();

                if (result.ContainsKey("response_status") && 
                    result["response_status"].ToString() != "success")
                {
                    var errorMessage = result.ContainsKey("error_message") ? 
                        result["error_message"].ToString() : "Unknown error";
                    throw new Exception(errorMessage);
                }

                return result;
            }
            catch (Exception ex) when (!(ex is Exception))
            {
                throw new Exception($"API call failed: {ex.Message}", ex);
            }
        }
    }
}
#endif