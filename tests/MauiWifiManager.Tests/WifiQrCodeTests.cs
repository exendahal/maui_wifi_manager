using MauiWifiManager.Abstractions;

namespace MauiWifiManager.Tests;

public class WifiQrCodeTests
{
    [Fact]
    public void Parse_ReadsAllFields()
    {
        var code = WifiQrCode.Parse("WIFI:T:WPA;S:Home;P:secret;H:true;;");

        Assert.NotNull(code);
        Assert.Equal("Home", code.Ssid);
        Assert.Equal("secret", code.Password);
        Assert.Equal(WifiSecurityType.Wpa2Psk, code.SecurityType);
        Assert.True(code.IsHidden);
    }

    [Fact]
    public void Parse_FieldOrderAndPrefixCaseDoNotMatter()
    {
        var code = WifiQrCode.Parse("wifi:P:secret;S:Home;T:WPA;;");

        Assert.NotNull(code);
        Assert.Equal("Home", code.Ssid);
        Assert.Equal("secret", code.Password);
    }

    [Theory]
    [InlineData("WPA", WifiSecurityType.Wpa2Psk)]
    [InlineData("wpa", WifiSecurityType.Wpa2Psk)]
    [InlineData("SAE", WifiSecurityType.Wpa3Sae)]
    [InlineData("WPA3", WifiSecurityType.Wpa3Sae)]
    [InlineData("WEP", WifiSecurityType.Wep)]
    [InlineData("nopass", WifiSecurityType.Open)]
    public void Parse_MapsSecurityType(string type, WifiSecurityType expected)
    {
        var code = WifiQrCode.Parse($"WIFI:T:{type};S:Net;P:pw;;");

        Assert.Equal(expected, code!.SecurityType);
    }

    [Fact]
    public void Parse_MissingTypeAndPassword_IsOpen()
    {
        Assert.Equal(WifiSecurityType.Open, WifiQrCode.Parse("WIFI:S:Cafe;;")!.SecurityType);
    }

    [Fact]
    public void Parse_MissingTypeWithPassword_IsWpa2()
    {
        Assert.Equal(WifiSecurityType.Wpa2Psk, WifiQrCode.Parse("WIFI:S:Cafe;P:pw;;")!.SecurityType);
    }

    [Fact]
    public void Parse_UnescapesSpecialCharacters()
    {
        var code = WifiQrCode.Parse(@"WIFI:T:WPA;S:My\;Net\:1;P:a\\b\,c\""d;;");

        Assert.Equal("My;Net:1", code!.Ssid);
        Assert.Equal("a\\b,c\"d", code.Password);
    }

    [Fact]
    public void Parse_HiddenDefaultsToFalse()
    {
        Assert.False(WifiQrCode.Parse("WIFI:T:WPA;S:Home;P:pw;;")!.IsHidden);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://example.com")]
    [InlineData("WIFI:T:WPA;P:pw;;")]  // no SSID
    [InlineData("WIFI:T:WPA;S:;P:pw;;")] // empty SSID
    public void Parse_InvalidPayload_ReturnsNull(string? text)
    {
        Assert.Null(WifiQrCode.Parse(text));
    }

    [Fact]
    public void ToString_WritesStandardPayload()
    {
        var payload = new WifiQrCode("Home", "secret", WifiSecurityType.Wpa2Psk).ToString();

        Assert.Equal("WIFI:T:WPA;S:Home;P:secret;;", payload);
    }

    [Fact]
    public void ToString_OpenNetwork_OmitsPassword()
    {
        Assert.Equal("WIFI:T:nopass;S:Cafe;;", new WifiQrCode("Cafe", "ignored", WifiSecurityType.Open).ToString());
    }

    [Fact]
    public void ToString_HiddenWpa3_WritesSaeAndHiddenFlag()
    {
        Assert.Equal("WIFI:T:SAE;S:Lab;P:pw;H:true;;", new WifiQrCode("Lab", "pw", WifiSecurityType.Wpa3Sae, isHidden: true).ToString());
    }

    [Fact]
    public void ToString_EscapesSpecialCharacters()
    {
        var payload = new WifiQrCode("My;Net", "a:b", WifiSecurityType.Wpa2Psk).ToString();

        Assert.Equal(@"WIFI:T:WPA;S:My\;Net;P:a\:b;;", payload);
    }

    [Theory]
    [InlineData("Home", "secret", WifiSecurityType.Wpa2Psk, false)]
    [InlineData("Café ☕", "p@ss word", WifiSecurityType.Wpa3Sae, true)]
    [InlineData(@"we\ird;:,""name", @"p\;:,""w", WifiSecurityType.Wep, false)]
    [InlineData("Guest", "", WifiSecurityType.Open, true)]
    public void ToString_ThenParse_RoundTrips(string ssid, string password, WifiSecurityType security, bool hidden)
    {
        var original = new WifiQrCode(ssid, password, security, hidden);

        var parsed = WifiQrCode.Parse(original.ToString());

        Assert.NotNull(parsed);
        Assert.Equal(ssid, parsed.Ssid);
        Assert.Equal(password, parsed.Password);
        Assert.Equal(security, parsed.SecurityType);
        Assert.Equal(hidden, parsed.IsHidden);
    }

    [Fact]
    public void ToConnectionOptions_CopiesSecurityAndHidden()
    {
        var options = new WifiQrCode("Lab", "pw", WifiSecurityType.Wpa3Sae, isHidden: true).ToConnectionOptions();

        Assert.Equal(WifiSecurityType.Wpa3Sae, options.SecurityType);
        Assert.True(options.IsHidden);
        Assert.Null(options.Bssid);
    }
}
