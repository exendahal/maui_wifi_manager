using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;

namespace WifiManager.MacVendors
{
    /// <summary>
    /// Names the manufacturer of a network adapter from its MAC address, offline, using the IEEE MA-L
    /// registry (about 40,000 prefixes) bundled in this package.
    /// </summary>
    public static class MacVendorRegistry
    {
        private const string ResourceName = "WifiManager.MacVendors.oui.tsv.gz";

        // Read on first use (tens of milliseconds), on whichever thread asks first.
        private static readonly Lazy<Dictionary<int, string>> Table = new Lazy<Dictionary<int, string>>(Load);

        /// <summary>
        /// The manufacturer for a MAC address such as <c>3C:CD:57:73:6E:E2</c>, <c>3c-cd-57-73-6e-e2</c>,
        /// <c>3ccd.5773.6ee2</c> or <c>3CCD57736EE2</c>, e.g. <c>Xiaomi Mobile Software</c>. Returns
        /// <see langword="null"/> for unknown prefixes, malformed input and private (randomized) addresses,
        /// which carry no manufacturer.
        /// </summary>
        public static string? Lookup(string? macAddress)
        {
            if (!TryGetPrefix(macAddress, out var prefix))
                return null;

            // Locally administered addresses (phones' private MACs) are not assigned by IEEE.
            if (((prefix >> 16) & 0x02) != 0)
                return null;

            return Table.Value.TryGetValue(prefix, out var vendor) ? vendor : null;
        }

        /// <summary>How many prefixes the registry holds.</summary>
        public static int Count => Table.Value.Count;

        /// <summary>The first three bytes of a 6-byte MAC address, or false when it is not one.</summary>
        internal static bool TryGetPrefix(string? macAddress, out int prefix)
        {
            prefix = 0;
            if (macAddress == null)
                return false;

            var digits = 0;
            foreach (var c in macAddress)
            {
                if (c == ':' || c == '-' || c == '.')
                    continue;
                int value;
                if (c >= '0' && c <= '9')
                    value = c - '0';
                else if (c >= 'a' && c <= 'f')
                    value = c - 'a' + 10;
                else if (c >= 'A' && c <= 'F')
                    value = c - 'A' + 10;
                else
                    return false;
                if (digits < 6)
                    prefix = (prefix << 4) | value;
                digits++;
            }
            return digits == 12;
        }

        private static Dictionary<int, string> Load()
        {
            using var stream = typeof(MacVendorRegistry).Assembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException($"{ResourceName} is missing from the package.");
            using var gzip = new GZipStream(stream, CompressionMode.Decompress);
            return Read(gzip);
        }

        /// <summary>Reads <c>PREFIX&lt;TAB&gt;Vendor</c> lines, skipping any that are malformed.</summary>
        internal static Dictionary<int, string> Read(Stream stream)
        {
            var table = new Dictionary<int, string>(40_000);
            using var reader = new StreamReader(stream);
            // Thousands of prefixes share a maker (Apple has 1,500); keep one copy of each name.
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length < 8 || line[6] != '\t'
                    || !int.TryParse(line.Substring(0, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var prefix))
                    continue;
                var name = line.Substring(7);
                if (!names.TryGetValue(name, out var shared))
                    names[name] = shared = name;
                table[prefix] = shared;
            }
            return table;
        }
    }
}
