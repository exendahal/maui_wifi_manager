using System;
using System.Net;

namespace MauiWifiManager.Abstractions
{
    /// <summary>
    /// Represents information about a Wi-Fi network.
    /// </summary>
    public class NetworkData
    {
        /// <summary>
        /// Gets or sets the status identifier of the network connection.
        /// </summary>
        public int StatusId { get; set; }

        /// <summary>
        /// Gets or sets the Service Set Identifier (SSID) of the Wi-Fi network.
        /// </summary>
        public string? Ssid { get; set; }

        /// <summary>
        /// Gets or sets the IP address as an integer in network byte order.
        /// Use <see cref="GetIpAddressString"/> to convert to a readable format.
        /// </summary>
        public int IpAddress { get; set; }

        /// <summary>
        /// Gets or sets the gateway address as an integer in network byte order.
        /// Use <see cref="GetGatewayAddressString"/> to convert to a readable format.
        /// </summary>
        public int GatewayAddress { get; set; }

        /// <summary>
        /// Gets or sets the DHCP server address as an integer in network byte order.
        /// Supported on Android and Windows only.
        /// Use <see cref="GetDhcpServerAddressString"/> to convert to a readable format.
        /// </summary>
        public int DhcpServerAddress { get; set; }

        /// <summary>
        /// Gets or sets the platform-specific native network object.
        /// </summary>
        public object? NativeObject { get; set; }

        /// <summary>
        /// Gets or sets the Basic Service Set Identifier (BSSID) of the Wi-Fi network.
        /// This is typically the MAC address of the access point.
        /// </summary>
        public object? Bssid { get; set; }

        /// <summary>
        /// Gets or sets the signal strength of the Wi-Fi network.
        /// The format varies by platform (RSSI on Android/iOS, bars on Windows).
        /// </summary>
        public object? SignalStrength { get; set; }

        /// <summary>
        /// Gets or sets the security type of the Wi-Fi network (e.g., WPA2, WPA3, Open).
        /// </summary>
        public object? SecurityType { get; set; }

        /// <summary>
        /// Converts the IP address to a human-readable string format (e.g., "192.168.1.100").
        /// </summary>
        /// <returns>The IP address as a string, or an empty string if the address is invalid.</returns>
        public string GetIpAddressString()
        {
            return ConvertIntToIpString(IpAddress);
        }

        /// <summary>
        /// Converts the gateway address to a human-readable string format.
        /// </summary>
        /// <returns>The gateway address as a string, or an empty string if the address is invalid.</returns>
        public string GetGatewayAddressString()
        {
            return ConvertIntToIpString(GatewayAddress);
        }

        /// <summary>
        /// Converts the DHCP server address to a human-readable string format.
        /// </summary>
        /// <returns>The DHCP server address as a string, or an empty string if the address is invalid.</returns>
        public string GetDhcpServerAddressString()
        {
            return ConvertIntToIpString(DhcpServerAddress);
        }

        private static string ConvertIntToIpString(int address)
        {
            if (address == 0)
                return string.Empty;

            try
            {
                byte[] bytes = BitConverter.GetBytes(address);
                var ipAddress = new IPAddress(bytes);
                return ipAddress.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    /// <summary>
    /// Represents a response from a Wi-Fi manager operation.
    /// </summary>
    /// <typeparam name="T">The type of data returned in the response.</typeparam>
    public class WifiManagerResponse<T>
    {
        /// <summary>
        /// Gets or sets the data returned from the operation.
        /// </summary>
        public T? Data { get; set; }

        /// <summary>
        /// Gets or sets the error code indicating the result of the operation.
        /// </summary>
        public WifiErrorCodes? ErrorCode { get; set; }

        /// <summary>
        /// Gets or sets a human-readable error or success message.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets a value indicating whether the operation was successful.
        /// </summary>
        public bool IsSuccess => ErrorCode == WifiErrorCodes.Success;

        /// <summary>
        /// Creates a successful response with data and an optional message.
        /// </summary>
        /// <param name="data">The data to include in the response.</param>
        /// <param name="message">An optional success message. Defaults to "Operation completed successfully."</param>
        /// <returns>A <see cref="WifiManagerResponse{T}"/> indicating success.</returns>
        public static WifiManagerResponse<T> SuccessResponse(T? data, string? message = null)
        {
            return new WifiManagerResponse<T>
            {
                Data = data,
                ErrorCode = WifiErrorCodes.Success,
                ErrorMessage = message ?? "Operation completed successfully."
            };
        }

        /// <summary>
        /// Creates an error response with an error code and message.
        /// </summary>
        /// <param name="errorCode">The error code indicating the type of failure.</param>
        /// <param name="errorMessage">A human-readable error message.</param>
        /// <returns>A <see cref="WifiManagerResponse{T}"/> indicating failure.</returns>
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

    /// <summary>
    /// Defines error codes for Wi-Fi manager operations.
    /// </summary>
    public enum WifiErrorCodes
    {
        /// <summary>
        /// Wi-Fi is not enabled on the device.
        /// </summary>
        WifiNotEnabled = 0,

        /// <summary>
        /// The operation completed successfully.
        /// </summary>
        Success = 1,

        /// <summary>
        /// The required permissions were denied.
        /// </summary>
        PermissionDenied = 2,

        /// <summary>
        /// No Wi-Fi connection is available.
        /// </summary>
        NoConnection = 3,

        /// <summary>
        /// The device does not have the required hardware support.
        /// </summary>
        UnsupportedHardware = 4,

        /// <summary>
        /// The requested network is unavailable.
        /// </summary>
        NetworkUnavailable = 5,

        /// <summary>
        /// The operation timed out.
        /// </summary>
        OperationTimeout = 6,

        /// <summary>
        /// The provided credentials (SSID or password) are invalid.
        /// </summary>
        InvalidCredential = 7,

        /// <summary>
        /// An unknown error occurred.
        /// </summary>
        UnknownError = 8
    }
}
