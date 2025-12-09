# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Added
- **CancellationToken Support**: All async methods now accept an optional `CancellationToken` parameter for better cancellation handling.
- **WifiManagerOptions**: Configurable options for connection timeout, retry count, input validation, and more.
- **WifiLogger**: Centralized logging helper for consistent log formatting across platforms.
- **WifiValidationHelper**: Input validation for SSID and password to catch errors early.
- **WifiManagerExtensions**: Extension methods including:
  - `ConnectWifiWithRetry()`: Automatic retry logic for failed connections.
  - `IsConnectedToWifi()`: Quick check for Wi-Fi connectivity status.
  - `GetCurrentSsid()`: Get the SSID of the currently connected network.
  - `FilterBySignalStrength()`: Filter networks by signal strength threshold.
  - `FilterBySsidPattern()`: Filter networks by SSID pattern matching.
  - `SortBySignalStrength()`: Sort networks by signal strength.
  - `RemoveDuplicateSsids()`: Remove duplicate networks, keeping the strongest signal.
- **NetworkData Helper Methods**: Added `GetIpAddressString()`, `GetGatewayAddressString()`, and `GetDhcpServerAddressString()` for easy IP address formatting.
- **WifiManagerResponse.IsSuccess**: Boolean property for quick success checking.

### Changed
- **Fixed Async Void Methods**: `DisconnectWifi()` now returns `Task` instead of `void` for proper async patterns.
- **Enhanced XML Documentation**: All public APIs now have comprehensive documentation with parameter descriptions, return values, and exceptions.
- **Improved Error Codes Documentation**: All enum values now have descriptive documentation.
- **Options Support in UseMauiWifiManager()**: The initialization method now accepts optional `WifiManagerOptions` parameter.

### Improved
- **Input Validation**: SSID and password validation can be enabled via options (enabled by default).
- **Better Error Messages**: More descriptive error messages throughout the codebase.
- **Consistent Logging**: Standardized logging format with severity levels (INFO, WARNING, ERROR, DEBUG).
- **Code Documentation**: Added examples in XML documentation for better developer experience.

## Previous Versions

See the commit history for changes in previous versions.
