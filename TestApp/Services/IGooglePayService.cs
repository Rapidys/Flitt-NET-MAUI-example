using System.Text.RegularExpressions;

namespace TestApp.Services
{
    public interface IGooglePayService
    {
        Task<bool> IsReadyToPayAsync();

        // Original token-based method
        Task<GooglePayResult> InitializeGooglePayFromTokenAsync(string token);

        // New order-based methods (like Java SDK)
        Task<GooglePayResult> InitializeGooglePayAsync(Order order);
        Task<string> CreateOrderTokenAsync(Order order);
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

    public class Order
    {
        public enum Verification
        {
            Amount,
            Code
        }

        public enum Lang
        {
            Ru,
            Uk,
            En,
            Lv,
            Fr
        }

        public enum Currency
        {
            UAH,
            USD,
            EUR,
            GBP,
            GEL,
            RUB,
            KZT,
            CZK
        }

        // Required fields (readonly after construction)
        public int Amount { get; }
        public string CurrencyCode { get; }
        public string Id { get; }
        public string Description { get; }
        public string Email { get; }

        // Optional fields with setters
        public string ProductId { get; private set; }
        public string PaymentSystems { get; private set; }
        public string DefaultPaymentSystem { get; private set; }
        public int Lifetime { get; private set; } = -1;
        public string MerchantData { get; private set; }
        public bool Preauth { get; private set; } = false;
        public bool RequiredRecToken { get; private set; } = false;
        public bool VerificationEnabled { get; private set; } = false;
        public Verification VerificationType { get; private set; } = Verification.Amount;
        public string RecToken { get; private set; }
        public string Version { get; private set; }
        public Lang? Language { get; private set; }
        public string ServerCallbackUrl { get; private set; }
        public string ReservationData { get; private set; }
        public string PaymentSystem { get; private set; }
        public bool Delayed { get; private set; } = false;

        public Dictionary<string, string> Arguments { get; } = new Dictionary<string, string>();

        // Email validation regex (similar to Android Patterns.EMAIL_ADDRESS)
        private static readonly Regex EmailRegex = new Regex(
            @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
            RegexOptions.Compiled);

        // Constructor with Currency enum
        public Order(int amount, Currency currency, string id, string description)
            : this(amount, currency, id, description, null)
        {
        }

        public Order(int amount, Currency currency, string id, string description, string email)
            : this(amount, currency.ToString(), id, description, email)
        {
        }

        // Main constructor with string currency
        public Order(int amount, string currency, string id, string description, string email)
        {
            // Validation (matching Java SDK)
            if (amount <= 0)
                throw new ArgumentException("Amount should be more than 0");

            if (string.IsNullOrEmpty(currency))
                throw new ArgumentNullException(nameof(currency), "currency should be not null");

            if (string.IsNullOrEmpty(id))
                throw new ArgumentNullException(nameof(id), "id should be not null");

            if (id.Length == 0 || id.Length > 1024)
                throw new ArgumentException("id's length should be > 0 && <= 1024");

            if (string.IsNullOrEmpty(description))
                throw new ArgumentNullException(nameof(description), "description should be not null");

            if (description.Length == 0 || description.Length > 1024)
                throw new ArgumentException("description's length should be > 0 && <= 1024");

            if (!string.IsNullOrEmpty(email) && !EmailRegex.IsMatch(email))
                throw new ArgumentException("email is not valid");

            Amount = amount;
            CurrencyCode = currency;
            Id = id;
            Description = description;
            Email = email;
        }

        // Setter methods (matching Java SDK)
        public void SetProductId(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value), "ProductId should be not null");

            if (value.Length > 1024)
                throw new ArgumentException("ProductId should be not more than 1024 symbols");

            ProductId = value;
        }

        public void SetPaymentSystems(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value), "PaymentSystems should be not null");

            PaymentSystems = value;
        }

        public void SetDelayed(bool value)
        {
            Delayed = value;
        }

        public void SetDefaultPaymentSystem(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value), "Default payment system should be not null");

            DefaultPaymentSystem = value;
        }

        public void SetLifetime(int value)
        {
            Lifetime = value;
        }

        public void SetMerchantData(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value), "MerchantData should be not null");

            if (value.Length > 2048)
                throw new ArgumentException("MerchantData should be not more than 2048 symbols");

            MerchantData = value;
        }

        public void SetPreauth(bool enable)
        {
            Preauth = enable;
        }

        public void SetRequiredRecToken(bool enable)
        {
            RequiredRecToken = enable;
        }

        public void SetVerification(bool enable)
        {
            VerificationEnabled = enable;
        }

        public void SetVerificationType(Verification type)
        {
            VerificationType = type;
        }

        public void SetRecToken(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value), "RecToken should be not null");

            RecToken = value;
        }

        public void SetVersion(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value), "version should be not null");

            if (value.Length > 10)
                throw new ArgumentException("version should be not more than 10 symbols");

            Version = value;
        }

        public void SetLang(Lang value)
        {
            Language = value;
        }

        public void SetServerCallbackUrl(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value), "server callback url should be not null");

            if (value.Length > 2048)
                throw new ArgumentException("server callback url should be not more than 2048 symbols");

            ServerCallbackUrl = value;
        }

        public void SetReservationData(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value), "reservation data should be not null");

            ReservationData = value;
        }

        public void AddArgument(string name, string value)
        {
            Arguments[name] = value;
        }

        // Helper methods
        public static Order CreateTestOrder()
        {
            var amount = 100; // 1.00 GEL (amount in minor units)
            var email = "test@gmail.com";
            var description = "test payment";
            var currency = Currency.GEL;
            var orderId = $"aloevera_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

            return new Order(amount, currency, orderId, description, email);
        }

        public static Order CreateOrder(int amount, Currency currency, string description, string email = null)
        {
            var orderId = $"order_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            return new Order(amount, currency, orderId, description, email);
        }

        // Override ToString for debugging
        public override string ToString()
        {
            return $"Order[Id={Id}, Amount={Amount}, Currency={CurrencyCode}, Description={Description}]";
        }
    }

    public class GooglePayMetaInfo
    {
        public string Token { get; set; }
        public Order Order { get; set; }
        public int Amount { get; set; }
        public string Currency { get; set; }
        public string CallbackUrl { get; set; }

        public GooglePayMetaInfo(string token, Order order, int amount, string currency, string callbackUrl)
        {
            Token = token;
            Order = order;
            Amount = amount;
            Currency = currency;
            CallbackUrl = callbackUrl;
        }
    }
}