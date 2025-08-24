namespace MauiWifiManager.Abstractions
{
    public class NetworkData
    {
        public int StatusId { get; set; }
        public string? Ssid { get; set; }
        public int IpAddress { get; set; }
        public string? GatewayAddress { get; set; }

        /// <summary>
        ///Supported on Android and Windows only
        /// </summary>
        /// 
        public string? DhcpServerAddress { get; set; }
        public object? NativeObject { get; set; }
        public object? Bssid { get; set; }
        public object? SignalStrength { get; set; }
        public object? SecurityType { get; set; }
    }
    public class WifiManagerResponse<T>
    {
        public T? Data { get; set; }
        public WifiErrorCodes? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public static WifiManagerResponse<T> SuccessResponse(T? data, string errorMessage)
        {
            return new WifiManagerResponse<T>
            {
                Data = data,
                ErrorCode = WifiErrorCodes.Success,
                ErrorMessage = errorMessage
            };
        }

        public static WifiManagerResponse<T> ErrorResponse(WifiErrorCodes errorCode, string errorMessage)
        {
            return new WifiManagerResponse<T>
            {
                Data = default,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage
            };
        }

    }
    public enum WifiErrorCodes
    {
        WifiNotEnabled = 0,
        Success = 1,
        PermissionDenied = 2,
        NoConnection = 3,
        UnsupportedHardware = 4,
        NetworkUnavailable = 5,
        OperationTimeout = 6,
        InvalidCredential = 7,
        UnknownError = 8
    }
}
