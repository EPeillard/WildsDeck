using WildsDeck.Core;

namespace WildsDeck.Tests;

public sealed class ModeDebouncerTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-08-30T12:00:00Z");

    [Fact]
    public void UnknownTransitionsToTownAfterDebounce()
    {
        var detector = new ModeDebouncer(TimeSpan.FromSeconds(1));
        Assert.False(detector.Observe(GameMode.Town, Start, out _));
        Assert.True(detector.Observe(GameMode.Town, Start.AddSeconds(1), out GameMode previous));
        Assert.Equal(GameMode.Unknown, previous);
        Assert.Equal(GameMode.Town, detector.Current);
    }

    [Fact]
    public void TownTransitionsToHuntAfterDebounce()
    {
        var detector = new ModeDebouncer(TimeSpan.FromSeconds(1), GameMode.Town);
        Assert.False(detector.Observe(GameMode.Hunt, Start, out _));
        Assert.True(detector.Observe(GameMode.Hunt, Start.AddSeconds(1), out GameMode previous));
        Assert.Equal(GameMode.Town, previous);
        Assert.Equal(GameMode.Hunt, detector.Current);
    }

    [Fact]
    public void HuntTransitionsBackToTown()
    {
        var detector = new ModeDebouncer(TimeSpan.FromMilliseconds(750), GameMode.Hunt);
        detector.Observe(GameMode.Town, Start, out _);
        Assert.True(detector.Observe(GameMode.Town, Start.AddMilliseconds(750), out GameMode previous));
        Assert.Equal(GameMode.Hunt, previous);
        Assert.Equal(GameMode.Town, detector.Current);
    }

    [Fact]
    public void BriefTransientDoesNotChangePublishedMode()
    {
        var detector = new ModeDebouncer(TimeSpan.FromSeconds(1), GameMode.Town);
        detector.Observe(GameMode.Hunt, Start, out _);
        detector.Observe(GameMode.Town, Start.AddMilliseconds(400), out _);
        detector.Observe(GameMode.Hunt, Start.AddMilliseconds(500), out _);
        Assert.False(detector.Observe(GameMode.Hunt, Start.AddMilliseconds(1200), out _));
        Assert.Equal(GameMode.Town, detector.Current);
    }

    [Fact]
    public void UnknownSampleDoesNotForceAProfileTransition()
    {
        var detector = new ModeDebouncer(TimeSpan.FromSeconds(1), GameMode.Hunt);
        Assert.False(detector.Observe(GameMode.Unknown, Start, out _));
        Assert.Equal(GameMode.Hunt, detector.Current);
    }
}
