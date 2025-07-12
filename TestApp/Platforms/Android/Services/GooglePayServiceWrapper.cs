#if ANDROID
using Microsoft.Maui.Controls;
using System.Threading.Tasks;

namespace TestApp.Services
{
    public class GooglePayServiceWrapper : IGooglePayService
    {
        private GooglePayService _googlePayService;

        public GooglePayServiceWrapper()
        {
            var mainActivity = Platform.CurrentActivity as MainActivity;
            _googlePayService = mainActivity?.GetGooglePayService();
        }

        public async Task<bool> IsReadyToPayAsync()
        {
            if (_googlePayService == null)
                return false;
                
            return await _googlePayService.IsReadyToPayAsync();
        }

        public async Task<GooglePayResult> InitializeGooglePayFromTokenAsync(string token)
        {
            if (_googlePayService == null)
                return new GooglePayResult { Success = false, Error = "Google Pay service not initialized" };

            return await _googlePayService.InitializeGooglePayFromTokenAsync(token);
        }
    }
}
#endif