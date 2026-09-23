using GeoScenery.Api.Auth;
using Microsoft.Extensions.Caching.Memory;

namespace GeoScenery.Tests;

[TestFixture]
public sealed class LoginAttemptTrackerTests
{
    private LoginAttemptTracker _tracker = null!;

    [SetUp]
    public void SetUp()
    {
        _tracker = new LoginAttemptTracker(new MemoryCache(new MemoryCacheOptions()));
    }

    [Test]
    public void GivenFewFailures_WhenCheckingLockState_ThenTheAccountIsNotLocked()
    {
        for (var i = 0; i < 5; i++)
        {
            _tracker.RecordFailure("victim@example.com");
        }

        Assert.That(_tracker.IsLocked("victim@example.com"), Is.False);
    }

    [Test]
    public void GivenEnoughFailures_WhenCheckingLockState_ThenTheAccountIsLocked()
    {
        for (var i = 0; i < 10; i++)
        {
            _tracker.RecordFailure("victim@example.com");
        }

        Assert.That(_tracker.IsLocked("victim@example.com"), Is.True);
    }

    [Test]
    public void GivenALockedAccount_WhenResetting_ThenTheAccountIsUnlocked()
    {
        for (var i = 0; i < 10; i++)
        {
            _tracker.RecordFailure("victim@example.com");
        }

        _tracker.Reset("victim@example.com");

        Assert.That(_tracker.IsLocked("victim@example.com"), Is.False);
    }

    [Test]
    public void GivenFailuresOnOneAccount_WhenCheckingAnother_ThenTheOtherAccountIsNotLocked()
    {
        for (var i = 0; i < 10; i++)
        {
            _tracker.RecordFailure("victim@example.com");
        }

        Assert.That(_tracker.IsLocked("another@example.com"), Is.False);
    }
}