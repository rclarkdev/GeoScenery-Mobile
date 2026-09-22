using GeoScenery.Data.Models;
using GeoScenery.Data.Services;

namespace GeoScenery.Tests;

[TestFixture]
public sealed class SceneServiceTests
{
    private TestDatabase _database = null!;
    private SceneService _service = null!;
    private User _owner = null!;

    [SetUp]
    public void SetUp()
    {
        _database = new TestDatabase();
        _service = new SceneService(_database.Context);
        _owner = new User { DisplayName = "Owner", Email = "owner@example.com", PasswordHash = "test" };
        _database.Context.Users.Add(_owner);
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

        var scenes = await _service.GetAllAsync();

        Assert.That(scenes.Select(scene => scene.Title), Is.EqualTo(new[] { "Alpine", "Zion" }));
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
        });

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
        });

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
        });

        var updated = await _service.UpdateAsync(scene.Id, new Scene
        {
            Title = "New title",
            Description = "New description",
            ImageUrl = "https://example.com/new.jpg",
            Rating = 10
        }, _owner.Id);

        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Title, Is.EqualTo("New title"));
        Assert.That(updated.Rating, Is.EqualTo(10));
    }

    [Test]
    public async Task GivenAMissingSceneId_WhenUpdatingOrDeletingTheScene_ThenNoChangeIsMade()
    {
        var updated = await _service.UpdateAsync(404, new Scene { Title = "Missing" }, _owner.Id);
        var deleted = await _service.DeleteAsync(404, _owner.Id);

        Assert.That(updated, Is.Null);
        Assert.That(deleted, Is.False);
    }

    [Test]
    public async Task GivenAnExistingScene_WhenDeletingTheScene_ThenTheSceneNoLongerExists()
    {
        var scene = await _service.CreateAsync(new Scene { Title = "Temporary", OwnerUserId = _owner.Id });

        var deleted = await _service.DeleteAsync(scene.Id, _owner.Id);

        Assert.That(deleted, Is.True);
        Assert.That(await _service.GetByIdAsync(scene.Id), Is.Null);
    }

    [Test]
    public async Task GivenARecordOwnedByAnotherUser_WhenUpdatingOrDeletingTheScene_ThenNoChangeIsMade()
    {
        var scene = await _service.CreateAsync(new Scene { Title = "Protected", OwnerUserId = _owner.Id });

        var updated = await _service.UpdateAsync(scene.Id, new Scene { Title = "Hijacked" }, 999);
        var deleted = await _service.DeleteAsync(scene.Id, 999);

        Assert.That(updated, Is.Null);
        Assert.That(deleted, Is.False);
        Assert.That((await _service.GetByIdAsync(scene.Id))?.Title, Is.EqualTo("Protected"));
    }
}
