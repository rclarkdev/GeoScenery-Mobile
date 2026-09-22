using GeoScenery.Data.Models;
using GeoScenery.Data.Services;

namespace GeoScenery.Tests;

[TestFixture]
public sealed class SceneServiceTests
{
    private TestDatabase _database = null!;
    private SceneService _service = null!;
    private User _owner = null!;
    private User _otherUser = null!;

    [SetUp]
    public void SetUp()
    {
        _database = new TestDatabase();
        _service = new SceneService(_database.Context);
        _owner = new User { DisplayName = "Owner", Email = "owner@example.com", PasswordHash = "test" };
        _otherUser = new User { DisplayName = "Other", Email = "other@example.com", PasswordHash = "test" };
        _database.Context.Users.AddRange(_owner, _otherUser);
        _database.Context.SaveChanges();
    }

    [TearDown]
    public void TearDown() => _database.Dispose();

    [Test]
    public async Task GivenScenesWithDifferentTitles_WhenGettingAllScenes_ThenScenesAreOrderedByTitle()
    {
        _database.Context.Scenes.AddRange(
            new Scene { Title = "Zion", Description = "Z", ImageUrl = "https://example.com/z.jpg", Rating = 8 },
            new Scene { Title = "Alpine", Description = "A", ImageUrl = "https://example.com/a.jpg", Rating = 9 });
        await _database.Context.SaveChangesAsync();

        var scenes = await _service.SearchAsync(null, null, null, null);

        Assert.That(scenes.Select(result => result.Scene.Title), Is.EqualTo(new[] { "Alpine", "Zion" }));
    }

    [Test]
    public async Task GivenAnExistingSceneId_WhenGettingTheScene_ThenTheSceneIsReturned()
    {
        var scene = await _service.CreateAsync(new Scene
        {
            Title = "Observation Point",
            Description = "A scenic view.",
            ImageUrl = "https://example.com/view.jpg",
            Rating = 9
        }, null);

        var result = await _service.GetByIdAsync(scene.Id);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Title, Is.EqualTo("Observation Point"));
    }

    [Test]
    public async Task GivenValidSceneData_WhenCreatingAScene_ThenTimestampsAreSetAndTheSceneIsPersisted()
    {
        var before = DateTimeOffset.UtcNow;
        var scene = await _service.CreateAsync(new Scene
        {
            Title = "New scene",
            Description = "Description",
            ImageUrl = "https://example.com/new.jpg",
            Rating = 7
        }, null);

        Assert.That(scene.Id, Is.GreaterThan(0));
        Assert.That(scene.CreatedAt, Is.GreaterThanOrEqualTo(before));
        Assert.That(scene.UpdatedAt, Is.EqualTo(scene.CreatedAt));
    }

    [Test]
    public async Task GivenAnExistingScene_WhenUpdatingTheScene_ThenTheEditableFieldsAreChanged()
    {
        var scene = await _service.CreateAsync(new Scene
        {
            Title = "Old title",
            Description = "Old description",
            ImageUrl = "https://example.com/old.jpg",
            Rating = 5,
            OwnerUserId = _owner.Id
        }, null);

        var updated = await _service.UpdateAsync(scene.Id, new Scene
        {
            Title = "New title",
            Description = "New description",
            ImageUrl = "https://example.com/new.jpg",
            Rating = 10
        }, _owner.Id, null);

        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Title, Is.EqualTo("New title"));
        Assert.That(updated.Rating, Is.EqualTo(10));
    }

    [Test]
    public async Task GivenAMissingSceneId_WhenUpdatingOrDeletingTheScene_ThenNoChangeIsMade()
    {
        var updated = await _service.UpdateAsync(404, new Scene { Title = "Missing" }, _owner.Id, null);
        var deleted = await _service.DeleteAsync(404, _owner.Id);

        Assert.That(updated, Is.Null);
        Assert.That(deleted, Is.False);
    }

    [Test]
    public async Task GivenAnExistingScene_WhenDeletingTheScene_ThenTheSceneNoLongerExists()
    {
        var scene = await _service.CreateAsync(new Scene { Title = "Temporary", OwnerUserId = _owner.Id }, null);

        var deleted = await _service.DeleteAsync(scene.Id, _owner.Id);

        Assert.That(deleted, Is.True);
        Assert.That(await _service.GetByIdAsync(scene.Id), Is.Null);
    }

    [Test]
    public async Task GivenARecordOwnedByAnotherUser_WhenUpdatingOrDeletingTheScene_ThenNoChangeIsMade()
    {
        var scene = await _service.CreateAsync(new Scene { Title = "Protected", OwnerUserId = _owner.Id }, null);

        var updated = await _service.UpdateAsync(scene.Id, new Scene { Title = "Hijacked" }, 999, null);
        var deleted = await _service.DeleteAsync(scene.Id, 999);

        Assert.That(updated, Is.Null);
        Assert.That(deleted, Is.False);
        Assert.That((await _service.GetByIdAsync(scene.Id))?.Title, Is.EqualTo("Protected"));
    }

    [Test]
    public async Task GivenTagsWithMixedCaseAndDuplicates_WhenCreatingAScene_ThenTagsAreNormalizedAndDeduplicated()
    {
        var scene = await _service.CreateAsync(new Scene { Title = "Tagged", OwnerUserId = _owner.Id },
            ["Sunset", " sunset ", "Hiking"]);

        Assert.That(scene.Tags.Select(tag => tag.Tag).OrderBy(tag => tag), Is.EqualTo(new[] { "hiking", "sunset" }));
    }

    [Test]
    public async Task GivenAnExistingSceneWithTags_WhenUpdatingWithNewTags_ThenOldTagsAreReplaced()
    {
        var scene = await _service.CreateAsync(new Scene { Title = "Tagged", OwnerUserId = _owner.Id }, ["old-tag"]);

        var updated = await _service.UpdateAsync(scene.Id, new Scene { Title = "Tagged" }, _owner.Id, ["new-tag"]);

        Assert.That(updated!.Tags.Select(tag => tag.Tag), Is.EqualTo(new[] { "new-tag" }));
    }

    [Test]
    public async Task GivenAnExistingSceneWithTags_WhenUpdatingWithNullTags_ThenExistingTagsAreUnchanged()
    {
        var scene = await _service.CreateAsync(new Scene { Title = "Tagged", OwnerUserId = _owner.Id }, ["keep-me"]);

        var updated = await _service.UpdateAsync(scene.Id, new Scene { Title = "Tagged" }, _owner.Id, null);

        Assert.That(updated!.Tags.Select(tag => tag.Tag), Is.EqualTo(new[] { "keep-me" }));
    }

    [Test]
    public async Task GivenScenesWithDifferentTags_WhenSearchingByTag_ThenOnlyMatchingScenesAreReturned()
    {
        await _service.CreateAsync(new Scene { Title = "Beach", OwnerUserId = _owner.Id }, ["beach", "sunset"]);
        await _service.CreateAsync(new Scene { Title = "Mountain", OwnerUserId = _owner.Id }, ["hiking"]);

        var results = await _service.SearchAsync(["sunset"], null, null, null);

        Assert.That(results.Select(result => result.Scene.Title), Is.EqualTo(new[] { "Beach" }));
    }

    [Test]
    public async Task GivenScenesAtKnownLocations_WhenSearchingByLocationAndRadius_ThenOnlyScenesWithinRadiusAreReturnedOrderedByDistance()
    {
        // Roughly 1.1km and 111km north of the search origin, respectively
        await _service.CreateAsync(new Scene { Title = "Near", OwnerUserId = _owner.Id, Latitude = 0.01, Longitude = 0 }, null);
        await _service.CreateAsync(new Scene { Title = "Far", OwnerUserId = _owner.Id, Latitude = 1, Longitude = 0 }, null);
        await _service.CreateAsync(new Scene { Title = "NoLocation", OwnerUserId = _owner.Id }, null);

        var results = await _service.SearchAsync(null, 0, 0, 10);

        Assert.That(results.Select(result => result.Scene.Title), Is.EqualTo(new[] { "Near" }));
        Assert.That(results[0].DistanceKm, Is.Not.Null.And.LessThan(10));
    }

    [Test]
    public async Task GivenNoRatingsYet_WhenRatingAScene_ThenARatingIsAdded()
    {
        var scene = await _service.CreateAsync(new Scene { Title = "Rated", OwnerUserId = _owner.Id }, null);

        var rated = await _service.RateAsync(scene.Id, _otherUser.Id, 8);

        Assert.That(rated, Is.Not.Null);
        Assert.That(rated!.Ratings.Single().Rating, Is.EqualTo(8));
        Assert.That(rated.Ratings.Single().UserId, Is.EqualTo(_otherUser.Id));
    }

    [Test]
    public async Task GivenAnExistingRatingFromTheSameUser_WhenRatingAgain_ThenTheRatingIsUpdatedNotDuplicated()
    {
        var scene = await _service.CreateAsync(new Scene { Title = "Rated", OwnerUserId = _owner.Id }, null);
        await _service.RateAsync(scene.Id, _otherUser.Id, 5);

        var rated = await _service.RateAsync(scene.Id, _otherUser.Id, 9);

        Assert.That(rated!.Ratings.Count, Is.EqualTo(1));
        Assert.That(rated.Ratings.Single().Rating, Is.EqualTo(9));
    }

    [Test]
    public async Task GivenAMissingScene_WhenRating_ThenNullIsReturned()
    {
        var rated = await _service.RateAsync(404, _otherUser.Id, 5);

        Assert.That(rated, Is.Null);
    }

    [Test]
    public async Task GivenAnExistingRating_WhenRemovingIt_ThenItIsGone()
    {
        var scene = await _service.CreateAsync(new Scene { Title = "Rated", OwnerUserId = _owner.Id }, null);
        await _service.RateAsync(scene.Id, _otherUser.Id, 5);

        var removed = await _service.RemoveRatingAsync(scene.Id, _otherUser.Id);

        Assert.That(removed, Is.True);
        Assert.That((await _service.GetByIdAsync(scene.Id))!.Ratings, Is.Empty);
    }

    [Test]
    public async Task GivenNoExistingRating_WhenRemovingIt_ThenItIsIdempotentAndReturnsTrueForAnExistingScene()
    {
        var scene = await _service.CreateAsync(new Scene { Title = "Rated", OwnerUserId = _owner.Id }, null);

        var removed = await _service.RemoveRatingAsync(scene.Id, _otherUser.Id);

        Assert.That(removed, Is.True);
    }

    [Test]
    public async Task GivenAMissingScene_WhenRemovingARating_ThenFalseIsReturned()
    {
        var removed = await _service.RemoveRatingAsync(404, _otherUser.Id);

        Assert.That(removed, Is.False);
    }
}
