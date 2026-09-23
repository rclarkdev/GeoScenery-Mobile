using System.Net;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GeoScenery.Api.ViewModels;
using GeoScenery.Api.Auth;
using GeoScenery.Data.Context;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace GeoScenery.Tests;

[TestFixture]
public sealed class GeoSceneryApiTests
{
    private GeoSceneryApiFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new GeoSceneryApiFactory();
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(1));
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static string CreateToken(long userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("development-only-change-this-key-before-deployment-geoscenery"));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: "GeoScenery",
            audience: "GeoScenery.Client",
            claims: [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)));
    }

    private async Task<AuthResponseModel> RegisterUserAsync(string displayName, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName,
            email,
            password = "Password123!"
        });
        return (await response.Content.ReadFromJsonAsync<AuthResponseModel>())!;
    }

    private async Task<SceneResponse> CreateSceneAsync(string title = "Observation Point", string[]? tags = null, double? latitude = null, double? longitude = null)
    {
        var response = await _client.PostAsJsonAsync("/api/scenes", new
        {
            title,
            description = "A scenic view.",
            imageUrl = "https://example.com/view.jpg",
            rating = 9,
            tags,
            latitude,
            longitude
        });
        return (await response.Content.ReadFromJsonAsync<SceneResponse>())!;
    }

    // ----- Scenes: basic CRUD -----

    [Test]
    public async Task GivenAHealthyApi_WhenCheckingLiveness_ThenTheApiReturnsOk()
    {
        var response = await _client.GetAsync("/health/live");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task GivenAnApiRequest_WhenItCompletes_ThenACorrelationIdAndSqlLogEntryAreCreated()
    {
        var response = await _client.GetAsync("/api/scenes");

        Assert.That(response.Headers.TryGetValues("X-Correlation-ID", out var values), Is.True);
        Assert.That(values!.Single(), Is.Not.Empty);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MyProjectDbContext>();
        Assert.That(await db.AppLogEntries.AnyAsync(log => log.EventName == "HttpRequest" && log.RequestPath == "/api/scenes"), Is.True);
    }

    [Test]
    public async Task GivenAReachableDatabase_WhenCheckingReadiness_ThenTheApiReturnsOk()
    {
        var response = await _client.GetAsync("/health/ready");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
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
    public async Task GivenNoAuthorizationHeader_WhenPostingAScene_ThenTheApiReturnsUnauthorized()
    {
        using var anonymousClient = _factory.CreateClient();

        var response = await anonymousClient.PostAsJsonAsync("/api/scenes", new
        {
            title = "Should fail",
            description = "No auth.",
            imageUrl = "https://example.com/view.jpg",
            rating = 5
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task GivenASceneOwnedByAnotherUser_WhenUpdatingIt_ThenTheApiReturnsNotFound()
    {
        var scene = await CreateSceneAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(999));

        var response = await _client.PutAsJsonAsync($"/api/scenes/{scene.Id}", new
        {
            title = "Hijacked",
            description = "Not yours.",
            imageUrl = "https://example.com/view.jpg",
            rating = 1
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GivenAnExistingScene_WhenUpdatingItsTags_ThenTheResponseReflectsTheReplacedTags()
    {
        var scene = await CreateSceneAsync(tags: ["old-tag"]);

        var response = await _client.PutAsJsonAsync($"/api/scenes/{scene.Id}", new
        {
            title = scene.Title,
            description = scene.Description,
            imageUrl = scene.ImageUrl,
            rating = scene.Rating,
            tags = new[] { "new-tag" }
        });
        var updated = await response.Content.ReadFromJsonAsync<SceneResponse>();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(updated!.Tags, Is.EqualTo(new[] { "new-tag" }));
    }

    [Test]
    public async Task GivenAMissingSceneId_WhenUpdatingIt_ThenTheApiReturnsNotFound()
    {
        var response = await _client.PutAsJsonAsync("/api/scenes/404", new
        {
            title = "Missing",
            description = "No such scene.",
            imageUrl = "https://example.com/view.jpg",
            rating = 5
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GivenAnExistingScene_WhenDeletingIt_ThenTheApiReturnsNoContentAndItIsGone()
    {
        var scene = await CreateSceneAsync();

        var response = await _client.DeleteAsync($"/api/scenes/{scene.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That((await _client.GetAsync($"/api/scenes/{scene.Id}")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GivenASceneOwnedByAnotherUser_WhenDeletingIt_ThenTheApiReturnsNotFound()
    {
        var scene = await CreateSceneAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(999));

        var response = await _client.DeleteAsync($"/api/scenes/{scene.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    // ----- Scenes: validation boundaries -----

    [Test]
    public async Task GivenATitleThatIsTooLong_WhenCreatingAScene_ThenTheApiReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/scenes", new
        {
            title = new string('a', 201),
            description = "A scenic view.",
            imageUrl = "https://example.com/view.jpg",
            rating = 5
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GivenAMissingTitle_WhenCreatingAScene_ThenTheApiReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/scenes", new
        {
            title = "",
            description = "A scenic view.",
            imageUrl = "https://example.com/view.jpg",
            rating = 5
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GivenARatingAboveTheMaximum_WhenCreatingAScene_ThenTheApiReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/scenes", new
        {
            title = "Out of range",
            description = "A scenic view.",
            imageUrl = "https://example.com/view.jpg",
            rating = 11
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GivenARatingBelowTheMinimum_WhenCreatingAScene_ThenTheApiReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/scenes", new
        {
            title = "Out of range",
            description = "A scenic view.",
            imageUrl = "https://example.com/view.jpg",
            rating = -1
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    // ----- Scenes: tags and location search -----

    [Test]
    public async Task GivenASceneWithTags_WhenCreatingIt_ThenTheResponseIncludesNormalizedTags()
    {
        var scene = await CreateSceneAsync(tags: ["Sunset", " sunset ", "Hiking"]);

        Assert.That(scene.Tags, Is.EqualTo(new[] { "hiking", "sunset" }));
    }

    [Test]
    public async Task GivenScenesWithDifferentTags_WhenSearchingByTag_ThenOnlyMatchingScenesAreReturned()
    {
        await CreateSceneAsync("Beach", tags: ["beach"]);
        await CreateSceneAsync("Mountain", tags: ["hiking"]);

        var response = await _client.GetAsync("/api/scenes?tags=beach");
        var scenes = await response.Content.ReadFromJsonAsync<List<SceneResponse>>();

        Assert.That(scenes!.Select(scene => scene.Title), Is.EqualTo(new[] { "Beach" }));
    }

    [Test]
    public async Task GivenScenesAtKnownLocations_WhenSearchingByLocationAndRadius_ThenOnlyNearbyScenesAreReturnedWithDistance()
    {
        await CreateSceneAsync("Near", latitude: 0.01, longitude: 0);
        await CreateSceneAsync("Far", latitude: 1, longitude: 0);

        var response = await _client.GetAsync("/api/scenes?latitude=0&longitude=0&radiusKm=10");
        var scenes = await response.Content.ReadFromJsonAsync<List<SceneResponse>>();

        Assert.That(scenes!.Select(scene => scene.Title), Is.EqualTo(new[] { "Near" }));
        Assert.That(scenes![0].DistanceKm, Is.Not.Null);
    }

    // ----- Scenes: ratings -----

    [Test]
    public async Task GivenAnotherUsersScene_WhenRatingIt_ThenTheAverageAndCountAreUpdated()
    {
        var scene = await CreateSceneAsync();
        var rater = await RegisterUserAsync("Rater", "rater@example.com");
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(rater.UserId));

        var response = await _client.PostAsJsonAsync($"/api/scenes/{scene.Id}/rating", new { rating = 8 });
        var rated = await response.Content.ReadFromJsonAsync<SceneResponse>();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(rated!.AverageRating, Is.EqualTo(8));
        Assert.That(rated.RatingCount, Is.EqualTo(1));
        Assert.That(rated.CurrentUserRating, Is.EqualTo(8));
    }

    [Test]
    public async Task GivenYourOwnScene_WhenRatingIt_ThenTheApiReturnsBadRequest()
    {
        var scene = await CreateSceneAsync();

        var response = await _client.PostAsJsonAsync($"/api/scenes/{scene.Id}/rating", new { rating = 5 });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GivenAnExistingRating_WhenRemovingIt_ThenTheApiReturnsNoContent()
    {
        var scene = await CreateSceneAsync();
        var rater = await RegisterUserAsync("Rater", "rater2@example.com");
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(rater.UserId));
        await _client.PostAsJsonAsync($"/api/scenes/{scene.Id}/rating", new { rating = 5 });

        var response = await _client.DeleteAsync($"/api/scenes/{scene.Id}/rating");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task GivenARatingAboveTheMaximum_WhenRatingAScene_ThenTheApiReturnsBadRequest()
    {
        var scene = await CreateSceneAsync();
        var rater = await RegisterUserAsync("Rater", "rater3@example.com");
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(rater.UserId));

        var response = await _client.PostAsJsonAsync($"/api/scenes/{scene.Id}/rating", new { rating = 11 });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GivenAMissingSceneId_WhenRatingIt_ThenTheApiReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync("/api/scenes/404/rating", new { rating = 5 });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    // ----- Auth -----

    [Test]
    public async Task GivenAnExistingEmail_WhenRequestingPasswordReset_ThenTheApiReturnsAcceptedMessageAndSendsAResetLink()
    {
        await RegisterUserAsync("Reset User", "reset@example.com");

        var response = await _client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "reset@example.com" });
        var result = await response.Content.ReadFromJsonAsync<PasswordResetResponse>();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result?.Message, Does.Contain("If an account exists"));
        Assert.That(_factory.LastResetUrl, Does.Contain("token="));
    }

    [Test]
    public async Task GivenAnUnknownEmail_WhenRequestingPasswordReset_ThenTheApiReturnsTheSameGenericMessage()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "unknown@example.com" });
        var result = await response.Content.ReadFromJsonAsync<PasswordResetResponse>();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result?.Message, Does.Contain("If an account exists"));
        Assert.That(_factory.LastResetUrl, Is.Null);
    }

    [Test]
    public async Task GivenAValidResetLink_WhenResettingThePassword_ThenTheNewPasswordCanBeUsedOnce()
    {
        await RegisterUserAsync("Reset User", "reset-once@example.com");
        await _client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "reset-once@example.com" });
        var token = _factory.LastResetUrl!.Split("token=", StringSplitOptions.None)[1];

        var resetResponse = await _client.PostAsJsonAsync("/api/auth/reset-password", new { token, password = "NewPassword123!" });
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new { email = "reset-once@example.com", password = "NewPassword123!" });
        var reusedResponse = await _client.PostAsJsonAsync("/api/auth/reset-password", new { token, password = "AnotherPassword123!" });

        Assert.That(resetResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(loginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(reusedResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GivenValidRegistrationData_WhenRegistering_ThenTheApiReturnsCreatedUserData()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = "Ava",
            email = "ava@example.com",
            password = "Password123!"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var user = await response.Content.ReadFromJsonAsync<AuthResponseModel>();
        Assert.That(user?.Email, Is.EqualTo("ava@example.com"));
    }

    [Test]
    public async Task GivenAnAlreadyRegisteredEmail_WhenRegisteringAgain_ThenTheApiReturnsConflict()
    {
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = "Ava",
            email = "duplicate@example.com",
            password = "Password123!"
        });

        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = "Someone else",
            email = "duplicate@example.com",
            password = "Password123!"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task GivenValidCredentials_WhenLoggingIn_ThenTheApiReturnsAToken()
    {
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = "Ava",
            email = "login@example.com",
            password = "Password123!"
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "login@example.com",
            password = "Password123!"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var auth = await response.Content.ReadFromJsonAsync<AuthResponseModel>();
        Assert.That(auth?.Token, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task GivenAnIncorrectPassword_WhenLoggingIn_ThenTheApiReturnsUnauthorized()
    {
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = "Ava",
            email = "wrongpass@example.com",
            password = "Password123!"
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "wrongpass@example.com",
            password = "WrongPassword!"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task GivenATooShortPassword_WhenRegistering_ThenTheApiReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = "Ava",
            email = "shortpass@example.com",
            password = "short"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GivenAnInvalidEmail_WhenRegistering_ThenTheApiReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = "Ava",
            email = "not-an-email",
            password = "Password123!"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    // ----- Auth: rate limiting -----

    [Test]
    public async Task GivenAHandfulOfFailedLogins_ThenASubsequentCorrectLoginStillWorks()
    {
        await RegisterUserAsync("Retry", "retry@example.com");

        for (var i = 0; i < 5; i++)
        {
            var failed = await _client.PostAsJsonAsync("/api/auth/login", new
            {
                email = "retry@example.com",
                password = $"WrongPassword!{i}"
            });
            Assert.That(failed.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        var succeeded = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "retry@example.com",
            password = "Password123!"
        });
        Assert.That(succeeded.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task GivenRepeatedLoginAttempts_WhenExceedingThePerIpLimit_ThenTheApiReturnsTooManyRequests()
    {
        await RegisterUserAsync("Throttled", "throttled@example.com");

        for (var i = 0; i < 20; i++)
        {
            var attempt = await _client.PostAsJsonAsync("/api/auth/login", new
            {
                email = "throttled@example.com",
                password = $"WrongPassword!{i}"
            });
            Assert.That(attempt.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        var exceeded = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "throttled@example.com",
            password = "Password123!"
        });
        Assert.That(exceeded.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
    }

    [Test]
    public async Task GivenAnAccountLockedByRepeatedFailures_WhenLoggingInWithTheCorrectPassword_ThenTheApiStillReturnsUnauthorized()
    {
        await RegisterUserAsync("Locked", "locked@example.com");

        for (var i = 0; i < 10; i++)
        {
            var attempt = await _client.PostAsJsonAsync("/api/auth/login", new
            {
                email = "locked@example.com",
                password = $"WrongPassword!{i}"
            });
            Assert.That(attempt.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        var correctPassword = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "locked@example.com",
            password = "Password123!"
        });
        Assert.That(correctPassword.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task GivenARegistrationBelowTheThreshold_WhenRegistering_ThenTheApiSucceeds()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = "Below Limit",
            email = "below@example.com",
            password = "Password123!"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task GivenRepeatedRegistrationAttempts_WhenExceedingThePerIpLimit_ThenTheApiReturnsTooManyRequests()
    {
        for (var i = 0; i < 5; i++)
        {
            var attempt = await _client.PostAsJsonAsync("/api/auth/register", new
            {
                displayName = $"Spam {i}",
                email = $"spam{i}@example.com",
                password = "Password123!"
            });
            Assert.That(attempt.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        var exceeded = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = "Spam",
            email = "spam-over@example.com",
            password = "Password123!"
        });
        Assert.That(exceeded.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
    }

    [Test]
    public async Task GivenAnExhaustedLoginBudget_WhenCallingAnUnrelatedEndpoint_ThenTheUnrelatedEndpointStillWorks()
    {
        for (var i = 0; i < 20; i++)
        {
            await _client.PostAsJsonAsync("/api/auth/login", new
            {
                email = "nobody@example.com",
                password = "WrongPassword!"
            });
        }

        var blockedLogin = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "nobody@example.com",
            password = "WrongPassword!"
        });
        Assert.That(blockedLogin.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));

        var scenes = await _client.GetAsync("/api/scenes");
        Assert.That(scenes.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task GivenRepeatedForgotPasswordRequests_WhenExceedingTheLimit_ThenTheApiReturnsTooManyRequests()
    {
        for (var i = 0; i < 5; i++)
        {
            var attempt = await _client.PostAsJsonAsync("/api/auth/forgot-password", new { email = $"missing{i}@example.com" });
            Assert.That(attempt.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        var exceeded = await _client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "missing-over@example.com" });
        Assert.That(exceeded.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
    }

    // ----- Follow/unfollow -----

    [Test]
    public async Task GivenAnotherUser_WhenFollowingThem_ThenFollowerAndFollowingCountsAreUpdated()
    {
        var other = await RegisterUserAsync("Other", "other@example.com");

        var response = await _client.PostAsync($"/api/users/{other.UserId}/follow", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var profile = await (await _client.GetAsync($"/api/users/{other.UserId}")).Content.ReadFromJsonAsync<UserResponse>();
        Assert.That(profile?.FollowerCount, Is.EqualTo(1));

        var me = await (await _client.GetAsync("/api/users/me")).Content.ReadFromJsonAsync<UserResponse>();
        Assert.That(me?.FollowingCount, Is.EqualTo(1));
    }

    [Test]
    public async Task GivenYourself_WhenFollowing_ThenTheApiReturnsBadRequest()
    {
        var response = await _client.PostAsync("/api/users/1/follow", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GivenAFollowedUser_WhenUnfollowing_ThenTheApiReturnsNoContentAndCountsReset()
    {
        var other = await RegisterUserAsync("Other", "unfollow@example.com");
        await _client.PostAsync($"/api/users/{other.UserId}/follow", null);

        var response = await _client.DeleteAsync($"/api/users/{other.UserId}/follow");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var profile = await (await _client.GetAsync($"/api/users/{other.UserId}")).Content.ReadFromJsonAsync<UserResponse>();
        Assert.That(profile?.FollowerCount, Is.EqualTo(0));
    }

    [Test]
    public async Task GivenAFollowedUser_WhenGettingTheirFollowers_ThenTheFollowingUserIsListed()
    {
        var other = await RegisterUserAsync("Other", "followers@example.com");
        await _client.PostAsync($"/api/users/{other.UserId}/follow", null);

        var response = await _client.GetAsync($"/api/users/{other.UserId}/followers");
        var followers = await response.Content.ReadFromJsonAsync<List<UserSummaryResponse>>();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(followers!.Select(user => user.Id), Does.Contain(1L));
    }

    [Test]
    public async Task GivenAnotherUsersProfile_WhenViewingIt_ThenEmailIsNotIncluded()
    {
        var other = await RegisterUserAsync("Other", "private@example.com");

        var response = await _client.GetAsync($"/api/users/{other.UserId}");
        var profile = await response.Content.ReadFromJsonAsync<UserResponse>();

        Assert.That(profile?.Email, Is.Null);
    }

    [Test]
    public async Task GivenYourOwnProfile_WhenViewingIt_ThenEmailIsIncluded()
    {
        var response = await _client.GetAsync("/api/users/me");
        var profile = await response.Content.ReadFromJsonAsync<UserResponse>();

        Assert.That(profile?.Email, Is.Not.Null);
    }

    [Test]
    public async Task GivenYourOwnProfile_WhenUpdatingIt_ThenTheProfileDetailFieldsArePersisted()
    {
        var response = await _client.PutAsJsonAsync("/api/users/1", new
        {
            displayName = "Updated Name",
            email = "updated@example.com",
            bio = "Loves scenic views.",
            education = "State University",
            hobbies = "Hiking, photography",
            employment = "Photographer",
            latitude = 12.5,
            longitude = -45.5
        });
        var updated = await response.Content.ReadFromJsonAsync<UserResponse>();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(updated?.DisplayName, Is.EqualTo("Updated Name"));
        Assert.That(updated?.Bio, Is.EqualTo("Loves scenic views."));
        Assert.That(updated?.Education, Is.EqualTo("State University"));
        Assert.That(updated?.Latitude, Is.EqualTo(12.5));
    }

    [Test]
    public async Task GivenAnotherUsersProfile_WhenUpdatingIt_ThenTheApiReturnsNotFound()
    {
        var other = await RegisterUserAsync("Other", "notmine@example.com");

        var response = await _client.PutAsJsonAsync($"/api/users/{other.UserId}", new
        {
            displayName = "Hijacked",
            email = "hijacked@example.com"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GivenAnInvalidEmail_WhenUpdatingYourProfile_ThenTheApiReturnsBadRequest()
    {
        var response = await _client.PutAsJsonAsync("/api/users/1", new
        {
            displayName = "Ava",
            email = "not-an-email"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GivenYourOwnAccount_WhenDeletingIt_ThenTheApiReturnsNoContent()
    {
        var user = await RegisterUserAsync("Deletable", "deletable@example.com");
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(user.UserId));

        var response = await _client.DeleteAsync($"/api/users/{user.UserId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task GivenAnotherUsersAccount_WhenDeletingIt_ThenTheApiReturnsNotFound()
    {
        var other = await RegisterUserAsync("Other", "notdeletable@example.com");

        var response = await _client.DeleteAsync($"/api/users/{other.UserId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    // ----- Visits -----

    [Test]
    public async Task GivenValidVisitData_WhenPostingAVisit_ThenTheApiReturnsCreatedVisitData()
    {
        var user = await RegisterUserAsync("Visitor", "visitor@example.com");
        var scene = await CreateSceneAsync("Scenic point");

        var response = await _client.PostAsJsonAsync("/api/visits", new
        {
            userId = user.UserId,
            sceneId = scene.Id
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var visit = await response.Content.ReadFromJsonAsync<TestVisitResponse>();
        Assert.That(visit?.SceneId, Is.EqualTo(scene.Id));
        Assert.That(visit?.UserId, Is.EqualTo(1));
    }

    private sealed record AuthResponseModel(long UserId, string DisplayName, string Email, string Token);
    private sealed record TestVisitResponse(long Id, long SceneId, long UserId, DateTimeOffset VisitedAt);
}
