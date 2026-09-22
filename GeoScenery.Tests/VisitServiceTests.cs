using GeoScenery.Data.Models;
using GeoScenery.Data.Services;

namespace GeoScenery.Tests;

[TestFixture]
public sealed class VisitServiceTests
{
    private TestDatabase _database = null!;
    private VisitService _service = null!;
    private User _user = null!;
    private Scene _scene = null!;

    [SetUp]
    public async Task SetUp()
    {
        _database = new TestDatabase();
        _service = new VisitService(_database.Context);
        _user = new User { DisplayName = "Visitor", Email = "visitor@example.com" };
        _scene = new Scene { Title = "Scenic point" };
        _database.Context.Users.Add(_user);
        _database.Context.Scenes.Add(_scene);
        await _database.Context.SaveChangesAsync();
    }

    [TearDown]
    public void TearDown() => _database.Dispose();

    [Test]
    public async Task GivenAVisitWithoutATimestamp_WhenCreatingTheVisit_ThenTheCurrentTimeIsAssigned()
    {
        var visit = await _service.CreateAsync(new Visit { UserId = _user.Id, SceneId = _scene.Id });

        Assert.That(visit.Id, Is.GreaterThan(0));
        Assert.That(visit.VisitedAt, Is.Not.EqualTo(default(DateTimeOffset)));
    }

    [Test]
    public async Task GivenVisitsForAUser_WhenGettingVisitsByUser_ThenOnlyThatUsersVisitsAreReturned()
    {
        await _service.CreateAsync(new Visit { UserId = _user.Id, SceneId = _scene.Id });

        var visits = await _service.GetByUserIdAsync(_user.Id);

        Assert.That(visits, Has.Count.EqualTo(1));
        Assert.That(visits[0].Scene.Title, Is.EqualTo("Scenic point"));
    }

    [Test]
    public async Task GivenAnExistingVisit_WhenGettingTheVisit_ThenTheSceneRelationshipIsIncluded()
    {
        var visit = await _service.CreateAsync(new Visit { UserId = _user.Id, SceneId = _scene.Id });

        var result = await _service.GetByIdAsync(visit.Id);

        Assert.That(result?.Scene.Title, Is.EqualTo("Scenic point"));
    }

    [Test]
    public async Task GivenAnExistingVisit_WhenUpdatingTheVisit_ThenItsReferencesAndTimestampAreChanged()
    {
        var secondScene = new Scene { Title = "Second point" };
        _database.Context.Scenes.Add(secondScene);
        await _database.Context.SaveChangesAsync();
        var visit = await _service.CreateAsync(new Visit { UserId = _user.Id, SceneId = _scene.Id });
        var visitedAt = DateTimeOffset.UtcNow.AddDays(-1);

        var updated = await _service.UpdateAsync(visit.Id, new Visit
        {
            UserId = _user.Id,
            SceneId = secondScene.Id,
            VisitedAt = visitedAt
        });

        Assert.That(updated?.SceneId, Is.EqualTo(secondScene.Id));
        Assert.That(updated?.VisitedAt, Is.EqualTo(visitedAt));
    }

    [Test]
    public async Task GivenAnExistingVisit_WhenDeletingTheVisit_ThenTheVisitNoLongerExists()
    {
        var visit = await _service.CreateAsync(new Visit { UserId = _user.Id, SceneId = _scene.Id });

        var deleted = await _service.DeleteAsync(visit.Id);

        Assert.That(deleted, Is.True);
        Assert.That(await _service.GetByIdAsync(visit.Id), Is.Null);
    }
}
