using System;

namespace MauiWifiManager.Helpers
{
    /// <summary>
    /// Provides validation methods for Wi-Fi operations.
    /// </summary>
    internal static class WifiValidationHelper
    {
        /// <summary>
        /// Validates SSID input.
        /// </summary>
        /// <param name="ssid">The SSID to validate.</param>
        /// <exception cref="ArgumentNullException">Thrown when SSID is null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown when SSID exceeds maximum length.</exception>
        public static void ValidateSsid(string? ssid)
        {
            if (string.IsNullOrWhiteSpace(ssid))
            {
                throw new ArgumentNullException(nameof(ssid), "SSID cannot be null or empty.");
            }

            // Maximum SSID length is 32 bytes per IEEE 802.11 standard
            if (ssid.Length > 32)
            {
                throw new ArgumentException("SSID cannot exceed 32 characters.", nameof(ssid));
            }
        }

        /// <summary>
        /// Validates password input.
        /// </summary>
        /// <param name="password">The password to validate.</param>
        /// <param name="allowEmpty">Whether to allow empty passwords (for open networks).</param>
        /// <exception cref="ArgumentNullException">Thrown when password is null and not allowed to be empty.</exception>
        /// <exception cref="ArgumentException">Thrown when password length is invalid for WPA/WPA2.</exception>
        public static void ValidatePassword(string? password, bool allowEmpty = false)
        {
            if (string.IsNullOrEmpty(password))
            {
                if (!allowEmpty)
                {
                    throw new ArgumentNullException(nameof(password), "Password cannot be null or empty.");
                }
                return;
            }

            // WPA/WPA2 password must be between 8-63 characters
            if (password.Length < 8)
            {
                throw new ArgumentException("Password must be at least 8 characters for WPA/WPA2 networks.", nameof(password));
            }

            if (password.Length > 63)
            {
                throw new ArgumentException("Password cannot exceed 63 characters.", nameof(password));
            }
        }

        /// <summary>
        /// Validates both SSID and password.
        /// </summary>
        /// <param name="ssid">The SSID to validate.</param>
        /// <param name="password">The password to validate.</param>
        /// <param name="allowEmptyPassword">Whether to allow empty passwords.</param>
        public static void ValidateCredentials(string? ssid, string? password, bool allowEmptyPassword = false)
        {
            ValidateSsid(ssid);
            ValidatePassword(password, allowEmptyPassword);
        }
    }
}
