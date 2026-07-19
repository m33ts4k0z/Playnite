using NUnit.Framework;
using Playnite.SDK;

namespace Playnite.Avalonia.WebView.Tests;

[TestFixture]
public sealed class WebViewContractTests
{
    [TestCase("store.steampowered.com", ".steampowered.com", true)]
    [TestCase("login.steampowered.com", "steampowered.com", true)]
    [TestCase("notsteampowered.com", "steampowered.com", false)]
    [TestCase("steampowered.com", "", false)]
    public void DomainMatchingHonorsCookieBoundaries(string host, string domain, bool expected)
    {
        Assert.That(WebViewPolicy.DomainMatches(host, domain), Is.EqualTo(expected));
    }

    [Test]
    public void PolicyRejectsJavaScriptDisableInsteadOfWeakeningIt()
    {
        var settings = new WebViewSettings { JavaScriptEnabled = false };
        Assert.Throws<NotSupportedException>(() => WebViewPolicy.Validate(settings));
    }

    [Test]
    public void PolicyRejectsUnsupportedResponseCapture()
    {
        var settings = new WebViewSettings { CaptureResponseContent = true };
        Assert.Throws<NotSupportedException>(() => WebViewPolicy.Validate(settings));
    }

    [TestCase(CookieSameSite.NoRestriction, 0)]
    [TestCase(CookieSameSite.LaxMode, 1)]
    [TestCase(CookieSameSite.StrictMode, 2)]
    public void SameSiteMappingRoundTrips(CookieSameSite sdkPolicy, int nativeValue)
    {
        var nativePolicy = (WebKitGtkCookieStore.SoupSameSitePolicy)nativeValue;
        Assert.Multiple(() =>
        {
            Assert.That(WebKitGtkCookieStore.MapSameSite(sdkPolicy), Is.EqualTo(nativePolicy));
            Assert.That(WebKitGtkCookieStore.MapSameSite(nativePolicy), Is.EqualTo(sdkPolicy));
        });
    }

    [Test]
    public void JavaScriptResultParsesGtkObjectJson()
    {
        var result = AvaloniaSdkWebView.ParseEvaluationResult(
            "{\"playniteSdkSuccess\":true,\"value\":42}");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Result, Is.EqualTo(42L));
        });
    }

    [Test]
    public void JavaScriptResultParsesWebView2QuotedJson()
    {
        var result = AvaloniaSdkWebView.ParseEvaluationResult(
            "\"{\\\"playniteSdkSuccess\\\":true,\\\"value\\\":\\\"ready\\\"}\"");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Result, Is.EqualTo("ready"));
        });
    }

    [Test]
    public void JavaScriptFailurePreservesEngineMessage()
    {
        var result = AvaloniaSdkWebView.ParseEvaluationResult(
            "{\"playniteSdkSuccess\":false,\"message\":\"boom\"}");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.EqualTo("boom"));
        });
    }
}
