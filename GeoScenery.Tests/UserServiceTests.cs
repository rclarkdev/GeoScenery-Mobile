using GeoScenery.Data.Models;
using GeoScenery.Data.Services;

namespace GeoScenery.Tests;

[TestFixture]
public sealed class UserServiceTests
{
    private TestDatabase _database = null!;
    private UserService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _database = new TestDatabase();
        _service = new UserService(_database.Context);
    }

    [TearDown]
    public void TearDown() => _database.Dispose();

    [Test]
    public async Task GivenUsersWithDifferentNames_WhenGettingAllUsers_ThenUsersAreOrderedByDisplayName()
    {
        _database.Context.Users.AddRange(
            new User { DisplayName = "Zoe", Email = "zoe@example.com" },
            new User { DisplayName = "Ava", Email = "ava@example.com" });
        await _database.Context.SaveChangesAsync();

        var users = await _service.GetAllAsync();

        Assert.That(users.Select(user => user.DisplayName), Is.EqualTo(new[] { "Ava", "Master", "Zoe" }));
    }

    [Test]
    public async Task GivenValidUserData_WhenCreatingAUser_ThenTheUserIsPersistedWithACreatedTimestamp()
    {
        var user = await _service.CreateAsync(new User { DisplayName = "Ava", Email = "ava@example.com" });

        Assert.That(user.Id, Is.GreaterThan(0));
        Assert.That(user.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
    }

    [Test]
    public async Task GivenAnExistingUser_WhenGettingTheUser_ThenTheUserIsReturned()
    {
        var user = await _service.CreateAsync(new User { DisplayName = "Ava", Email = "ava@example.com" });

        var result = await _service.GetByIdAsync(user.Id);

        Assert.That(result?.Email, Is.EqualTo("ava@example.com"));
    }

    [Test]
    public async Task GivenAnExistingUser_WhenUpdatingTheUser_ThenTheProfileFieldsAreChanged()
    {
        var user = await _service.CreateAsync(new User { DisplayName = "Old", Email = "old@example.com" });

        var updated = await _service.UpdateAsync(user.Id, new User { DisplayName = "New", Email = "new@example.com" });

        Assert.That(updated?.DisplayName, Is.EqualTo("New"));
        Assert.That(updated?.Email, Is.EqualTo("new@example.com"));
    }

    [Test]
    public async Task GivenAnExistingUser_WhenUpdatingProfileDetailFields_ThenAllFieldsArePersisted()
    {
        var user = await _service.CreateAsync(new User { DisplayName = "Ava", Email = "ava@example.com" });

        var updated = await _service.UpdateAsync(user.Id, new User
        {
            DisplayName = "Ava",
            Email = "ava@example.com",
            ProfileImageUrl = "data:image/png;base64,abc",
            Latitude = 12.5,
            Longitude = -45.5,
            BirthDate = new DateOnly(1990, 1, 1),
            Education = "State University",
            Hobbies = "Hiking, photography",
            Employment = "Photographer",
            Bio = "Loves scenic views."
        });

        Assert.That(updated?.ProfileImageUrl, Is.EqualTo("data:image/png;base64,abc"));
        Assert.That(updated?.Latitude, Is.EqualTo(12.5));
        Assert.That(updated?.Longitude, Is.EqualTo(-45.5));
        Assert.That(updated?.BirthDate, Is.EqualTo(new DateOnly(1990, 1, 1)));
        Assert.That(updated?.Education, Is.EqualTo("State University"));
        Assert.That(updated?.Hobbies, Is.EqualTo("Hiking, photography"));
        Assert.That(updated?.Employment, Is.EqualTo("Photographer"));
        Assert.That(updated?.Bio, Is.EqualTo("Loves scenic views."));
    }

    [Test]
    public async Task GivenAnExistingUser_WhenDeletingTheUser_ThenTheUserNoLongerExists()
    {
        var user = await _service.CreateAsync(new User { DisplayName = "Temporary", Email = "temporary@example.com" });

        var deleted = await _service.DeleteAsync(user.Id);

        Assert.That(deleted, Is.True);
        Assert.That(await _service.GetByIdAsync(user.Id), Is.Null);
    }
}
