using NUnit.Framework;
using Playnite.Avalonia.App.Services;

namespace Playnite.Avalonia.App.V7.Tests;

[TestFixture]
public sealed class AvaloniaSdkWebViewTests
{
    private static readonly Uri RequestedUri = new("https://www.google.com/search?q=playnite");

    [Test]
    public void InitialBlankCompletionCannotFinishRequestedNavigation()
    {
        Assert.That(
            AvaloniaSdkWebView.ShouldCompletePendingNavigation(
                RequestedUri,
                false,
                new Uri("about:blank")),
            Is.False);
    }

    [Test]
    public void RequestedNavigationCompletionFinishesWithoutStartingNotification()
    {
        Assert.That(
            AvaloniaSdkWebView.ShouldCompletePendingNavigation(
                RequestedUri,
                false,
                RequestedUri),
            Is.True);
    }

    [Test]
    public void RedirectCompletionFinishesAfterRequestedNavigationStarts()
    {
        Assert.That(
            AvaloniaSdkWebView.ShouldCompletePendingNavigation(
                RequestedUri,
                true,
                new Uri("https://consent.google.com/")),
            Is.True);
    }
}
