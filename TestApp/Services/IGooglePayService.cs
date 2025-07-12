namespace TestApp.Services
{
    public interface IGooglePayService
    {
        Task<bool> IsReadyToPayAsync();
        Task<GooglePayResult> InitializeGooglePayFromTokenAsync(string token);
    }

    public class GooglePayResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public string PaymentData { get; set; }
        public Receipt Receipt { get; set; }
    }

    public class FlittGooglePayConfig
    {
        public string PaymentSystem { get; set; }
        public string GooglePayData { get; set; }
        public string Token { get; set; }
        public string CallbackUrl { get; set; }
    }

    public class FlittGooglePayCall
    {
        public string Token { get; set; }
        public string PaymentSystem { get; set; }
        public string CallbackUrl { get; set; }
    }

    public class Receipt
    {
        public string MaskedCard { get; set; }
        public string CardBin { get; set; }
        public int Amount { get; set; }
        public int PaymentId { get; set; }
        public string Currency { get; set; }
        public string OrderStatus { get; set; }
        public string TransactionType { get; set; }
        public string RRN { get; set; }
        public string ApprovalCode { get; set; }
        public string ResponseCode { get; set; }
        public string PaymentSystem { get; set; }
        public string ResponseUrl { get; set; }
    }
}