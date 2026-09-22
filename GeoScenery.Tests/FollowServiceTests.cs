using GeoScenery.Data.Models;
using GeoScenery.Data.Services;

namespace GeoScenery.Tests;

[TestFixture]
public sealed class FollowServiceTests
{
    private TestDatabase _database = null!;
    private FollowService _service = null!;
    private User _alice = null!;
    private User _bob = null!;
    private User _carol = null!;

    [SetUp]
    public void SetUp()
    {
        _database = new TestDatabase();
        _service = new FollowService(_database.Context);
        _alice = new User { DisplayName = "Alice", Email = "alice@example.com", PasswordHash = "test" };
        _bob = new User { DisplayName = "Bob", Email = "bob@example.com", PasswordHash = "test" };
        _carol = new User { DisplayName = "Carol", Email = "carol@example.com", PasswordHash = "test" };
        _database.Context.Users.AddRange(_alice, _bob, _carol);
        _database.Context.SaveChanges();
    }

    [TearDown]
    public void TearDown() => _database.Dispose();

    [Test]
    public async Task GivenNoFollowRelationship_WhenCheckingIsFollowing_ThenReturnsFalse()
    {
        Assert.That(await _service.IsFollowingAsync(_alice.Id, _bob.Id), Is.False);
    }

    [Test]
    public async Task GivenAUserFollowsAnother_WhenCheckingIsFollowing_ThenReturnsTrue()
    {
        await _service.FollowAsync(_alice.Id, _bob.Id);

        Assert.That(await _service.IsFollowingAsync(_alice.Id, _bob.Id), Is.True);
        Assert.That(await _service.IsFollowingAsync(_bob.Id, _alice.Id), Is.False);
    }

    [Test]
    public async Task GivenAnAlreadyFollowedUser_WhenFollowingAgain_ThenNoDuplicateIsCreated()
    {
        await _service.FollowAsync(_alice.Id, _bob.Id);
        await _service.FollowAsync(_alice.Id, _bob.Id);

        Assert.That(await _service.GetFollowingCountAsync(_alice.Id), Is.EqualTo(1));
        Assert.That(await _service.GetFollowerCountAsync(_bob.Id), Is.EqualTo(1));
    }

    [Test]
    public async Task GivenAFollowedUser_WhenUnfollowing_ThenTheRelationshipIsRemoved()
    {
        await _service.FollowAsync(_alice.Id, _bob.Id);

        await _service.UnfollowAsync(_alice.Id, _bob.Id);

        Assert.That(await _service.IsFollowingAsync(_alice.Id, _bob.Id), Is.False);
    }

    [Test]
    public async Task GivenNoExistingFollow_WhenUnfollowing_ThenNoErrorOccurs()
    {
        Assert.DoesNotThrowAsync(async () => await _service.UnfollowAsync(_alice.Id, _bob.Id));
    }

    [Test]
    public async Task GivenMultipleFollowers_WhenGettingFollowerCounts_ThenCountsAreCorrect()
    {
        await _service.FollowAsync(_alice.Id, _carol.Id);
        await _service.FollowAsync(_bob.Id, _carol.Id);

        Assert.That(await _service.GetFollowerCountAsync(_carol.Id), Is.EqualTo(2));
        Assert.That(await _service.GetFollowingCountAsync(_alice.Id), Is.EqualTo(1));
    }

    [Test]
    public async Task GivenMultipleFollowers_WhenGettingFollowers_ThenUsersAreOrderedByDisplayName()
    {
        await _service.FollowAsync(_bob.Id, _carol.Id);
        await _service.FollowAsync(_alice.Id, _carol.Id);

        var followers = await _service.GetFollowersAsync(_carol.Id);

        Assert.That(followers.Select(user => user.DisplayName), Is.EqualTo(new[] { "Alice", "Bob" }));
    }

    [Test]
    public async Task GivenAUserFollowingOthers_WhenGettingFollowing_ThenUsersAreOrderedByDisplayName()
    {
        await _service.FollowAsync(_alice.Id, _carol.Id);
        await _service.FollowAsync(_alice.Id, _bob.Id);

        var following = await _service.GetFollowingAsync(_alice.Id);

        Assert.That(following.Select(user => user.DisplayName), Is.EqualTo(new[] { "Bob", "Carol" }));
    }
}
