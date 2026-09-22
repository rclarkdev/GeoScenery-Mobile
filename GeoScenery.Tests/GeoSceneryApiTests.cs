using System.Net;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace GeoScenery.Tests;

[TestFixture]
public sealed class GeoSceneryApiTests
{
    private GeoSceneryApiFactory _factory = null!;
    private HttpClient? _client;

    [SetUp]
    public void SetUp()
    {
        _factory = new GeoSceneryApiFactory();
        _client = _factory.CreateClient();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("development-only-change-this-key-before-deployment-geoscenery"));
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: "GeoScenery",
            audience: "GeoScenery",
            claims: [new Claim(ClaimTypes.NameIdentifier, "1")],
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)));
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task GivenAnEmptyDatabase_WhenGettingScenes_ThenTheApiReturnsAnEmptyOkList()
    {
        var response = await _client.GetAsync("/api/scenes");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadFromJsonAsync<List<SceneResponse>>(), Is.Empty);
    }

    [Test]
    public async Task GivenValidSceneData_WhenPostingAScene_ThenTheApiReturnsCreatedAndALocation()
    {
        var response = await _client.PostAsJsonAsync("/api/scenes", new
        {
            title = "Observation Point",
            description = "A scenic view.",
            imageUrl = "https://example.com/view.jpg",
            rating = 9
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(response.Headers.Location?.ToString(), Does.Match(@"/api/scenes/\d+"));
    }

    [Test]
    public async Task GivenAMissingSceneId_WhenGettingTheScene_ThenTheApiReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/scenes/404");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GivenValidUserData_WhenPostingAUser_ThenTheApiReturnsCreatedUserData()
    {
        var response = await _client.PostAsJsonAsync("/api/users", new
        {
            displayName = "Ava",
            email = "ava@example.com"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.That(user?.Email, Is.EqualTo("ava@example.com"));
    }

    [Test]
    public async Task GivenValidVisitData_WhenPostingAVisit_ThenTheApiReturnsCreatedVisitData()
    {
        var userResponse = await _client.PostAsJsonAsync("/api/users", new
        {
            displayName = "Visitor",
            email = "visitor@example.com"
        });
        var user = await userResponse.Content.ReadFromJsonAsync<UserResponse>();

        var sceneResponse = await _client.PostAsJsonAsync("/api/scenes", new
        {
            title = "Scenic point",
            description = "A view.",
            imageUrl = "https://example.com/view.jpg",
            rating = 8
        });
        var scene = await sceneResponse.Content.ReadFromJsonAsync<SceneResponse>();

        var response = await _client.PostAsJsonAsync("/api/visits", new
        {
            userId = user!.Id,
            sceneId = scene!.Id
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var visit = await response.Content.ReadFromJsonAsync<VisitResponse>();
        Assert.That(visit?.SceneId, Is.EqualTo(scene.Id));
        Assert.That(visit?.UserId, Is.EqualTo(user.Id));
    }

    private sealed record SceneResponse(long Id, string Title, string Description, string ImageUrl, decimal Rating, long? OwnerUserId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
    private sealed record UserResponse(long Id, string DisplayName, string Email, DateTimeOffset CreatedAt);
    private sealed record VisitResponse(long Id, long SceneId, long UserId, DateTimeOffset VisitedAt);
}
