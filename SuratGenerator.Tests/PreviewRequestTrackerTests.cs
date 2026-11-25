using SuratGenerator.Core.Services;

namespace SuratGenerator.Tests;

public sealed class PreviewRequestTrackerTests
{
    [Fact]
    public void NextMenghasilkanIdBerurutan()
    {
        var tracker = new PreviewRequestTracker();
        var first = tracker.Next();
        var second = tracker.Next();

        Assert.Equal(first + 1, second);
    }

    [Fact]
    public void RequestLamaDianggapTidakAktif()
    {
        var tracker = new PreviewRequestTracker();
        var first = tracker.Next();
        var second = tracker.Next();

        Assert.True(tracker.IsCurrent(second));
        Assert.False(tracker.IsCurrent(first));
    }
}
