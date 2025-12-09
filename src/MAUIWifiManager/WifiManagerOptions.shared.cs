using System;

namespace MauiWifiManager
{
    /// <summary>
    /// Configuration options for the Wi-Fi Manager.
    /// </summary>
    public class WifiManagerOptions
    {
        /// <summary>
        /// Gets or sets the default timeout for connection operations in seconds.
        /// Default is 30 seconds.
        /// </summary>
        public int ConnectionTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets the default timeout for scan operations in seconds.
        /// Default is 15 seconds.
        /// </summary>
        public int ScanTimeoutSeconds { get; set; } = 15;

        /// <summary>
        /// Gets or sets the number of retry attempts for failed connection operations.
        /// Default is 0 (no retries).
        /// </summary>
        public int ConnectionRetryCount { get; set; } = 0;

        /// <summary>
        /// Gets or sets the delay between retry attempts in milliseconds.
        /// Default is 1000 milliseconds (1 second).
        /// </summary>
        public int RetryDelayMilliseconds { get; set; } = 1000;

        /// <summary>
        /// Gets or sets whether to validate SSID and password inputs.
        /// Default is true.
        /// </summary>
        public bool ValidateInputs { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to allow empty passwords (for open networks).
        /// Default is false.
        /// </summary>
        public bool AllowEmptyPassword { get; set; } = false;

        /// <summary>
        /// Gets or sets the minimum signal strength threshold for automatic network selection.
        /// Value range: -100 (weakest) to 0 (strongest). Default is -70.
        /// Only applicable for platforms that support signal strength filtering.
        /// </summary>
        public int MinimumSignalStrength { get; set; } = -70;

        /// <summary>
        /// Gets the default options instance.
        /// </summary>
        public static WifiManagerOptions Default { get; } = new WifiManagerOptions();
    }
}
