using Halo.Shell;
using Xunit;

namespace Halo.Tests;

/// <summary>
/// The pill can be taken off screen for two unrelated reasons -- a fullscreen app is playing, or
/// the user asked for it to go away from the tray -- and they must not cancel each other out.
/// These pin down which reason wins, and that leaving either state costs exactly one render.
/// </summary>
public class NotchVisibilityTests
{
    [Fact]
    public void ANotificationLiftsTheFullscreenHide()
    {
        Assert.True(NotchVisibility.ShouldHide(userHidden: false, fullscreen: true, notifLive: false));
        Assert.False(NotchVisibility.ShouldHide(userHidden: false, fullscreen: true, notifLive: true));
    }

    [Fact]
    public void ANotificationDoesNotLiftTheUsersHide()
    {
        Assert.True(NotchVisibility.ShouldHide(userHidden: true, fullscreen: false, notifLive: true));
        Assert.True(NotchVisibility.ShouldHide(userHidden: true, fullscreen: true, notifLive: true));
    }

    [Fact]
    public void NothingToHideForLeavesTheFrameAlone()
    {
        Assert.False(NotchVisibility.ShouldHide(userHidden: false, fullscreen: false, notifLive: false));
    }

    [Fact]
    public void HidingHappensOnceAndThenTheFrameIsSkipped()
    {
        var first = NotchVisibility.Decide(shouldHide: true, hidden: false);
        Assert.Equal(NotchVisibilityAction.Hide, first.Action);
        Assert.True(first.ReturnEarly);
        Assert.True(first.Hidden);

        var next = NotchVisibility.Decide(shouldHide: true, hidden: first.Hidden);
        Assert.Equal(NotchVisibilityAction.None, next.Action);
        Assert.True(next.ReturnEarly);
        Assert.True(next.Hidden);
    }

    [Fact]
    public void ComingBackRendersOnceAndThenRunsNormally()
    {
        var back = NotchVisibility.Decide(shouldHide: false, hidden: true);
        Assert.Equal(NotchVisibilityAction.ShowAndRender, back.Action);
        Assert.False(back.ReturnEarly);
        Assert.False(back.Hidden);

        var next = NotchVisibility.Decide(shouldHide: false, hidden: back.Hidden);
        Assert.Equal(NotchVisibilityAction.None, next.Action);
        Assert.False(next.ReturnEarly);
        Assert.False(next.Hidden);
    }
}
