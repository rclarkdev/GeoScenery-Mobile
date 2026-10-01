using System.Net;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GeoScenery.Api.ViewModels;
using GeoScenery.Api.Auth;
using GeoScenery.Api.Logging;
using GeoScenery.Data.Context;
using GeoScenery.Data.Models;
using Microsoft.AspNetCore.Http;
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
            password = "Password123!",
            confirmPassword = "Password123!"
        });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        var verificationToken = _factory.LastVerificationUrl!.Split("token=", StringSplitOptions.None)[1];
        var verifyResponse = await _client.PostAsJsonAsync("/api/auth/verify-email", new { token = Uri.UnescapeDataString(verificationToken) });
        Assert.That(verifyResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "Password123!"
        });
        return (await loginResponse.Content.ReadFromJsonAsync<AuthResponseModel>())!;
    }

    private async Task<SceneResponse> CreateSceneAsync(string title = "Observation Point", string[]? tags = null, double? latitude = null, double? longitude = null)
    {
        var imageUrl = await UploadTestSceneImageAsync();
        var response = await _client.PostAsJsonAsync("/api/scenes", new
        {
            title,
            description = "A scenic view.",
            imageUrl,
            rating = 9,
            tags,
            latitude,
            longitude
        });
        return (await response.Content.ReadFromJsonAsync<SceneResponse>())!;
    }

    private async Task<string> UploadTestSceneImageAsync()
    {
        var imageBytes = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "TestAssets", "favicon.png"));
        using var content = new MultipartFormDataContent();
        using var image = new ByteArrayContent(imageBytes);
        image.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(image, "file", "test.png");
        content.Add(new StringContent("scene"), "kind");

        var response = await _client.PostAsync("/api/images", content);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Test image upload failed: {await response.Content.ReadAsStringAsync()}");
        }
        var uploaded = (await response.Content.ReadFromJsonAsync<UploadedImageResponse>())!;
        return uploaded.Url;
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
        var correlationId = values!.Single();
        Assert.That(correlationId, Is.Not.Empty);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MyProjectDbContext>();
        var entries = await db.AppLogEntries.AsNoTracking()
            .Where(log => log.CorrelationId == correlationId && log.EventName == "HttpRequest")
            .ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(entries, Has.Count.EqualTo(1), "Each request should produce exactly one database log row.");
            Assert.That(entries[0].RequestPath, Is.EqualTo("/api/scenes"));
            Assert.That(entries[0].StatusCode, Is.EqualTo((int)HttpStatusCode.OK));
            Assert.That(entries[0].DurationMilliseconds, Is.Not.Null);
            Assert.That(entries[0].PropertiesJson, Does.Contain("Success"));
        });
    }

    [Test]
    public async Task GivenAnApiRequestFails_WhenItReturnsNotFound_ThenOneFailureEntryIsStored()
    {
        var response = await _client.GetAsync("/api/scenes/987654321");
        var correlationId = response.Headers.GetValues("X-Correlation-ID").Single();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MyProjectDbContext>();
        var entries = await db.AppLogEntries.AsNoTracking()
            .Where(log => log.CorrelationId == correlationId)
            .ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(entries, Has.Count.EqualTo(1));
            Assert.That(entries[0].Level, Is.EqualTo("Warning"));
            Assert.That(entries[0].StatusCode, Is.EqualTo((int)HttpStatusCode.NotFound));
            Assert.That(entries[0].PropertiesJson, Does.Contain("Rejected"));
        });
    }

    [Test]
    public async Task GivenAuditPropertiesContainSecrets_WhenPersisted_ThenSecretsAndQueryStringsAreRedacted()
    {
        using var scope = _factory.Services.CreateScope();
        var auditLog = scope.ServiceProvider.GetRequiredService<ISqlAuditLog>();
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/auth/reset-password";
        context.Request.QueryString = new QueryString("?token=query-secret");
        context.Response.Headers["X-Correlation-ID"] = "redaction-test";

        await auditLog.WriteAsync(context, "Information", "RedactionTest", "Audit test.", properties:
            new Dictionary<string, object?>
            {
                ["outcome"] = "Success",
                ["resetToken"] = "body-secret"
            });

        using var verificationScope = _factory.Services.CreateScope();
        var db = verificationScope.ServiceProvider.GetRequiredService<MyProjectDbContext>();
        var entry = await db.AppLogEntries.AsNoTracking()
            .SingleAsync(log => log.CorrelationId == "redaction-test");

        Assert.Multiple(() =>
        {
            Assert.That(entry.RequestPath, Is.EqualTo("/auth/reset-password"));
            Assert.That(entry.PropertiesJson, Does.Contain("[REDACTED]"));
            Assert.That(entry.PropertiesJson, Does.Not.Contain("query-secret"));
            Assert.That(entry.PropertiesJson, Does.Not.Contain("body-secret"));
        });
    }

    [Test]
    public async Task GivenAReachableDatabase_WhenCheckingReadiness_ThenTheApiReturnsOk()
    {
        var response = await _client.GetAsync("/health/ready");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task GivenANewAccount_WhenEmailIsNotVerified_ThenLoginIsForbiddenUntilTheOneTimeLinkIsUsed()
    {
        var registration = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = "Unverified user",
            email = "unverified@example.com",
            password = "Password123!",
            confirmPassword = "Password123!"
        });
        var loginBeforeVerification = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "unverified@example.com",
            password = "Password123!"
        });
        var verificationToken = _factory.LastVerificationUrl!.Split("token=", StringSplitOptions.None)[1];
        var verify = await _client.PostAsJsonAsync("/api/auth/verify-email", new
        {
            token = Uri.UnescapeDataString(verificationToken)
        });
        var loginAfterVerification = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "unverified@example.com",
            password = "Password123!"
        });
        var reuseToken = await _client.PostAsJsonAsync("/api/auth/verify-email", new
        {
            token = Uri.UnescapeDataString(verificationToken)
        });

        Assert.Multiple(() =>
        {
            Assert.That(registration.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(loginBeforeVerification.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(verify.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(loginAfterVerification.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(reuseToken.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        });
    }

    [Test]
    public async Task GivenMismatchedRegistrationPasswords_WhenRegistering_ThenTheAccountIsRejected()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = "Mismatch",
            email = "mismatch@example.com",
            password = "Password123!",
            confirmPassword = "Different123!"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(_factory.LastVerificationUrl, Is.Null);
    }

    [Test]
    public async Task GivenARegisteredUser_WhenRequestingPasswordRecovery_ThenTheEmailLinkCanResetThePassword()
    {
        await RegisterUserAsync("Password recovery", "recovery@example.com");

        var forgotResponse = await _client.PostAsJsonAsync("/api/auth/forgot-password", new
        {
            email = "recovery@example.com"
        });
        var token = Uri.UnescapeDataString(_factory.LastResetUrl!.Split("token=", StringSplitOptions.None)[1]);
        var resetResponse = await _client.PostAsJsonAsync("/api/auth/reset-password", new
        {
            token,
            password = "NewPassword123!"
        });
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "recovery@example.com",
            password = "NewPassword123!"
        });

        Assert.Multiple(() =>
        {
            Assert.That(forgotResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(resetResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(loginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }

    [Test]
    public async Task GivenAnUnverifiedAccount_WhenPasswordIsResetThroughItsEmailLink_ThenTheAccountCanLogIn()
    {
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = "Unverified recovery",
            email = "unverified-recovery@example.com",
            password = "Password123!",
            confirmPassword = "Password123!"
        });
        await _client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "unverified-recovery@example.com" });
        var token = Uri.UnescapeDataString(_factory.LastResetUrl!.Split("token=", StringSplitOptions.None)[1]);

        var resetResponse = await _client.PostAsJsonAsync("/api/auth/reset-password", new
        {
            token,
            password = "NewPassword123!"
        });
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "unverified-recovery@example.com",
            password = "NewPassword123!"
        });

        Assert.That(resetResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(loginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task GivenARegularMember_WhenListingAdminUsers_ThenTheApiForbidsAccess()
    {
        var response = await _client.GetAsync("/api/admin/users");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task GivenTheConfiguredBootstrapAdmin_WhenAssigningRoles_ThenTheUserGetsAdminPermission()
    {
        var admin = await RegisterUserAsync("Admin", "admin@example.com");
        var member = await RegisterUserAsync("Member", "member@example.com");
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", admin.Token);

        var updateResponse = await _client.PutAsJsonAsync($"/api/admin/users/{member.UserId}/roles", new
        {
            roles = new[] { "Member", "Admin" }
        });
        var updatedUser = await updateResponse.Content.ReadFromJsonAsync<AdminUserResponse>();
        var adminClaims = new JwtSecurityTokenHandler().ReadJwtToken(admin.Token).Claims;

        Assert.Multiple(() =>
        {
            Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(updatedUser!.Roles, Does.Contain("Admin"));
            Assert.That(adminClaims, Has.Some.Matches<Claim>(claim => claim.Type == "permission" && claim.Value == "users.roles.manage"));
        });

        var memberLogin = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "member@example.com",
            password = "Password123!"
        });
        var memberAuth = await memberLogin.Content.ReadFromJsonAsync<AuthResponseModel>();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", memberAuth!.Token);
        var adminUsersResponse = await _client.GetAsync("/api/admin/users");

        Assert.That(adminUsersResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", admin.Token);
        var demoteResponse = await _client.PutAsJsonAsync($"/api/admin/users/{member.UserId}/roles", new
        {
            roles = new[] { "Member" }
        });
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", memberAuth.Token);
        var revokedAccessResponse = await _client.GetAsync("/api/admin/users");

        Assert.Multiple(() =>
        {
            Assert.That(demoteResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(revokedAccessResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        });
    }

    [Test]
    public async Task GivenTheOnlyAdmin_WhenRemovingTheirAdminRole_ThenTheApiRejectsTheChange()
    {
        var admin = await RegisterUserAsync("Admin", "admin@example.com");
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", admin.Token);

        var response = await _client.PutAsJsonAsync($"/api/admin/users/{admin.UserId}/roles", new
        {
            roles = new[] { "Member" }
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GivenTheOnlyAdmin_WhenDeletingThatAccount_ThenTheApiRejectsTheChange()
    {
        var admin = await RegisterUserAsync("Admin", "admin@example.com");
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", admin.Token);

        var response = await _client.DeleteAsync($"/api/admin/users/{admin.UserId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GivenAProfileReport_WhenAnAdminReviewsAndDeletesTheAccount_ThenTheReportIsResolvedAndRelatedRowsAreCleaned()
    {
        var admin = await RegisterUserAsync("Admin", "admin@example.com");
        var reportedUser = await RegisterUserAsync("Reported", "reported@example.com");
        Scene scene;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MyProjectDbContext>();
            scene = new Scene
            {
                Title = "Reported user scene",
                Description = "Owned by account to remove.",
                ImageUrl = "/uploads/admin-delete.jpg",
                OwnerUserId = reportedUser.UserId
            };
            db.Scenes.Add(scene);
            await db.SaveChangesAsync();
            db.Follows.Add(new Follow { FollowerId = 1, FollowingId = reportedUser.UserId });
            db.UserBlocks.Add(new UserBlock { BlockerId = reportedUser.UserId, BlockedId = 1 });
            db.Messages.Add(new Message
            {
                SenderId = reportedUser.UserId,
                RecipientId = 1,
                Body = "A test message."
            });
            db.Visits.Add(new Visit { UserId = reportedUser.UserId, SceneId = scene.Id });
            await db.SaveChangesAsync();
        }

        var reportResponse = await _client.PostAsJsonAsync($"/api/users/{reportedUser.UserId}/reports", new
        {
            description = "This profile is inappropriate."
        });
        var report = await reportResponse.Content.ReadFromJsonAsync<ContentReportResponse>()
            ?? throw new InvalidOperationException("The profile report was not returned.");

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", admin.Token);
        var listResponse = await _client.GetAsync("/api/admin/reports?status=Pending");
        if (listResponse.StatusCode != HttpStatusCode.OK)
        {
            var failureCorrelation = listResponse.Headers.GetValues("X-Correlation-ID").Single();
            using var failureScope = _factory.Services.CreateScope();
            var failureDb = failureScope.ServiceProvider.GetRequiredService<MyProjectDbContext>();
            var failureLog = await failureDb.AppLogEntries.AsNoTracking()
                .SingleAsync(entry => entry.CorrelationId == failureCorrelation);
            Assert.Fail($"Admin report list failed: {failureLog.PropertiesJson}");
        }
        var listedReports = await listResponse.Content.ReadFromJsonAsync<List<AdminContentReportResponse>>();
        var reviewResponse = await _client.PutAsJsonAsync($"/api/admin/reports/{report.Id}/status", new
        {
            status = "Reviewed",
            resolutionNotes = "Reviewed before action."
        });
        var deleteResponse = await _client.DeleteAsync($"/api/admin/users/{reportedUser.UserId}");

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MyProjectDbContext>();
        var persistedReport = await verifyDb.ContentReports.AsNoTracking().SingleAsync(candidate => candidate.Id == report.Id);
        var userExists = await verifyDb.Users.AnyAsync(user => user.Id == reportedUser.UserId);
        var hasFollows = await verifyDb.Follows.AnyAsync(follow => follow.FollowerId == reportedUser.UserId || follow.FollowingId == reportedUser.UserId);
        var hasBlocks = await verifyDb.UserBlocks.AnyAsync(block => block.BlockerId == reportedUser.UserId || block.BlockedId == reportedUser.UserId);
        var hasMessages = await verifyDb.Messages.AnyAsync(message => message.SenderId == reportedUser.UserId || message.RecipientId == reportedUser.UserId);
        var remainingSceneOwnerId = (await verifyDb.Scenes.SingleAsync(candidate => candidate.Id == scene.Id)).OwnerUserId;

        Assert.Multiple(() =>
        {
            Assert.That(reportResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(listResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(listedReports, Has.Some.Matches<AdminContentReportResponse>(candidate => candidate.Id == report.Id));
            Assert.That(reviewResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(persistedReport.Status, Is.EqualTo(ContentReportStatuses.Actioned));
            Assert.That(userExists, Is.False);
            Assert.That(hasFollows, Is.False);
            Assert.That(hasBlocks, Is.False);
            Assert.That(hasMessages, Is.False);
            Assert.That(remainingSceneOwnerId, Is.Null);
        });
    }

    [Test]
    public async Task GivenAProfileReport_WhenSubmittedWithADescription_ThenItIsStoredAndAdminsAreEmailed()
    {
        var admin = await RegisterUserAsync("Admin", "admin@example.com");
        var target = await RegisterUserAsync("Reported user", "reported@example.com");

        var response = await _client.PostAsJsonAsync($"/api/users/{target.UserId}/reports", new
        {
            description = "This profile contains targeted harassment."
        });
        var report = await response.Content.ReadFromJsonAsync<ContentReportResponse>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(report!.TargetType, Is.EqualTo(ContentReportTargets.Profile));
            Assert.That(report.TargetId, Is.EqualTo(target.UserId));
            Assert.That(_factory.ReportNotifications, Has.Count.EqualTo(1));
            Assert.That(_factory.ReportNotifications[0].Recipient, Is.EqualTo("admin@example.com"));
            Assert.That(_factory.ReportNotifications[0].Report.Description, Is.EqualTo("This profile contains targeted harassment."));
            Assert.That(_factory.ReportNotifications[0].Report.ReporterEmail, Is.EqualTo("test@example.com"));
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MyProjectDbContext>();
        Assert.That(await db.ContentReports.CountAsync(), Is.EqualTo(1));
        Assert.That(admin.UserId, Is.EqualTo(2));
    }

    [Test]
    public async Task GivenASceneReport_WhenSubmittedWithADescription_ThenAdminsAreEmailedTheSceneDetails()
    {
        var admin = await RegisterUserAsync("Admin", "admin@example.com");
        Scene scene;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MyProjectDbContext>();
            scene = new Scene
            {
                Title = "Reported overlook",
                Description = "A scene to report.",
                ImageUrl = "/uploads/reported.jpg",
                Rating = 0,
                OwnerUserId = admin.UserId
            };
            db.Scenes.Add(scene);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync($"/api/scenes/{scene.Id}/reports", new
        {
            description = "The uploaded image is explicit."
        });
        var report = await response.Content.ReadFromJsonAsync<ContentReportResponse>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(report!.TargetType, Is.EqualTo(ContentReportTargets.Scene));
            Assert.That(report.TargetId, Is.EqualTo(scene.Id));
            Assert.That(_factory.ReportNotifications, Has.Count.EqualTo(1));
            Assert.That(_factory.ReportNotifications[0].Recipient, Is.EqualTo("admin@example.com"));
            Assert.That(_factory.ReportNotifications[0].Report.TargetLabel, Is.EqualTo("Reported overlook"));
            Assert.That(_factory.ReportNotifications[0].Report.Description, Is.EqualTo("The uploaded image is explicit."));
        });
    }

    [Test]
    public async Task GivenReportEmailFails_WhenSubmittingAReport_ThenTheSingleRequestLogRecordsTheSideEffectFailure()
    {
        await RegisterUserAsync("Admin", "admin@example.com");
        var target = await RegisterUserAsync("Reported user", "reported@example.com");
        _factory.FailReportNotifications = true;

        var response = await _client.PostAsJsonAsync($"/api/users/{target.UserId}/reports", new
        {
            description = "Please review this profile."
        });
        var correlationId = response.Headers.GetValues("X-Correlation-ID").Single();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MyProjectDbContext>();
        var entries = await db.AppLogEntries.AsNoTracking()
            .Where(log => log.CorrelationId == correlationId)
            .ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(entries, Has.Count.EqualTo(1));
            Assert.That(entries[0].Level, Is.EqualTo("Warning"));
            Assert.That(entries[0].PropertiesJson, Does.Contain("failed-or-partial"));
            Assert.That(entries[0].PropertiesJson, Does.Contain("InvalidOperationException"));
        });
    }

    [Test]
    public async Task GivenAnEmptyDescription_WhenReportingAProfile_ThenTheApiRejectsTheReport()
    {
        var target = await RegisterUserAsync("Reported user", "reported@example.com");

        var response = await _client.PostAsJsonAsync($"/api/users/{target.UserId}/reports", new
        {
            description = "   "
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(_factory.ReportNotifications, Is.Empty);
    }

    [Test]
    public async Task GivenTheDefaultMasterAccount_WhenTheDatabaseIsCreated_ThenItHasTheAdminRole()
    {
        using var database = new TestDatabase();
        var role = await database.Context.UserRoles.AsNoTracking()
            .SingleAsync(userRole => userRole.UserId == 1);

        Assert.That(role.RoleName, Is.EqualTo(AppRoles.Admin));
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
        var imageUrl = await UploadTestSceneImageAsync();
        var response = await _client.PostAsJsonAsync("/api/scenes", new
        {
            title = "Observation Point",
            description = "A scenic view.",
            imageUrl,
            rating = 9
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(response.Headers.Location?.ToString(), Does.Match(@"/api/scenes/\d+"));
    }

    [Test]
    public async Task GivenASceneCreatedByAnotherUser_WhenListingAndFilteringScenes_ThenTheSceneIsReturned()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MyProjectDbContext>();
        var scene = new Scene
        {
            Title = "Shared overlook",
            Description = "A scenic view.",
            ImageUrl = "/uploads/00000000000000000000000000000000.jpg",
            Rating = 9,
            Latitude = 45,
            Longitude = -93,
            OwnerUserId = 1,
            Tags = [new SceneTag { Tag = "sunset" }]
        };
        db.Scenes.Add(scene);
        await db.SaveChangesAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(2));

        var allResponse = await _client.GetAsync("/api/scenes");
        var allScenes = await allResponse.Content.ReadFromJsonAsync<List<SceneResponse>>();
        var filteredResponse = await _client.GetAsync("/api/scenes?tags=sunset&latitude=45&longitude=-93&radiusKm=10");
        var filteredScenes = await filteredResponse.Content.ReadFromJsonAsync<List<SceneResponse>>();

        Assert.Multiple(() =>
        {
            Assert.That(allResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(allScenes, Has.Some.Matches<SceneResponse>(candidate => candidate.Id == scene.Id));
            Assert.That(filteredResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(filteredScenes, Has.Some.Matches<SceneResponse>(candidate => candidate.Id == scene.Id));
        });
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
            imageUrl = scene.ImageUrl,
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
        var imageUrl = await UploadTestSceneImageAsync();
        var response = await _client.PutAsJsonAsync("/api/scenes/404", new
        {
            title = "Missing",
            description = "No such scene.",
            imageUrl,
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
            password = "Password123!",
            confirmPassword = "Password123!"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var registration = await response.Content.ReadFromJsonAsync<RegistrationResponse>();
        Assert.That(registration?.Message, Does.Contain("verification link"));
        Assert.That(_factory.LastVerificationUrl, Does.Contain("token="));
    }

    [Test]
    public async Task GivenAnAlreadyRegisteredEmail_WhenRegisteringAgain_ThenTheApiReturnsConflict()
    {
        await RegisterUserAsync("Ava", "duplicate@example.com");

        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = "Someone else",
            email = "duplicate@example.com",
            password = "Password123!",
            confirmPassword = "Password123!"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        var correlationId = response.Headers.GetValues("X-Correlation-ID").Single();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MyProjectDbContext>();
        var auditEntries = await db.AppLogEntries.AsNoTracking()
            .Where(log => log.CorrelationId == correlationId)
            .ToListAsync();
        Assert.That(auditEntries, Has.Count.EqualTo(1));
        Assert.That(auditEntries[0].StatusCode, Is.EqualTo((int)HttpStatusCode.Conflict));
    }

    [Test]
    public async Task GivenValidCredentials_WhenLoggingIn_ThenTheApiReturnsAToken()
    {
        await RegisterUserAsync("Ava", "login@example.com");

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
        await RegisterUserAsync("Ava", "wrongpass@example.com");

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
            password = "short",
            confirmPassword = "short"
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
            password = "Password123!",
            confirmPassword = "Password123!"
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

        // RegisterUserAsync verifies the email then performs one successful login;
        // that consumes one request from this IP's 20-request login budget.
        for (var i = 0; i < 19; i++)
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
            password = "Password123!",
            confirmPassword = "Password123!"
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
                password = "Password123!",
                confirmPassword = "Password123!"
            });
            Assert.That(attempt.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        var exceeded = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = "Spam",
            email = "spam-over@example.com",
            password = "Password123!",
            confirmPassword = "Password123!"
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
    public async Task GivenUsersWhoFollowEachOther_WhenBlocking_ThenBothFollowsAreRemovedAndProfileShowsBlocked()
    {
        var other = await RegisterUserAsync("Blocked", "blocked@example.com");
        await _client.PostAsync($"/api/users/{other.UserId}/follow", null);
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", other.Token);
        await _client.PostAsync("/api/users/1/follow", null);
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(1));

        var firstBlock = await _client.PostAsync($"/api/users/{other.UserId}/block", null);
        var repeatedBlock = await _client.PostAsync($"/api/users/{other.UserId}/block", null);
        var profile = await (await _client.GetAsync($"/api/users/{other.UserId}")).Content.ReadFromJsonAsync<UserResponse>();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MyProjectDbContext>();
        var followsRemain = await db.Follows.AnyAsync(follow =>
            (follow.FollowerId == 1 && follow.FollowingId == other.UserId)
            || (follow.FollowerId == other.UserId && follow.FollowingId == 1));

        Assert.Multiple(() =>
        {
            Assert.That(firstBlock.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(repeatedBlock.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(profile?.IsBlockedByCurrentUser, Is.True);
            Assert.That(profile?.HasBlockedCurrentUser, Is.False);
            Assert.That(profile?.IsFollowedByCurrentUser, Is.False);
            Assert.That(followsRemain, Is.False);
        });
    }

    [Test]
    public async Task GivenABlockedUser_WhenUnblocking_ThenFollowCanBeCreatedAgain()
    {
        var other = await RegisterUserAsync("Unblocked", "unblocked@example.com");
        await _client.PostAsync($"/api/users/{other.UserId}/block", null);

        var firstUnblock = await _client.DeleteAsync($"/api/users/{other.UserId}/block");
        var repeatedUnblock = await _client.DeleteAsync($"/api/users/{other.UserId}/block");
        var follow = await _client.PostAsync($"/api/users/{other.UserId}/follow", null);
        var profile = await (await _client.GetAsync($"/api/users/{other.UserId}")).Content.ReadFromJsonAsync<UserResponse>();

        Assert.Multiple(() =>
        {
            Assert.That(firstUnblock.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(repeatedUnblock.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(follow.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(profile?.IsBlockedByCurrentUser, Is.False);
            Assert.That(profile?.IsFollowedByCurrentUser, Is.True);
        });
    }

    [Test]
    public async Task GivenTheOtherUserBlockedYou_WhenViewingAndFollowing_ThenProfileShowsStateAndFollowIsRejected()
    {
        var other = await RegisterUserAsync("Blocker", "blocker@example.com");
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", other.Token);
        await _client.PostAsync("/api/users/1/block", null);
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(1));

        var profile = await (await _client.GetAsync($"/api/users/{other.UserId}")).Content.ReadFromJsonAsync<UserResponse>();
        var follow = await _client.PostAsync($"/api/users/{other.UserId}/follow", null);

        Assert.Multiple(() =>
        {
            Assert.That(profile?.IsBlockedByCurrentUser, Is.False);
            Assert.That(profile?.HasBlockedCurrentUser, Is.True);
            Assert.That(follow.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        });
    }

    [Test]
    public async Task GivenYouBlockedTheOtherUser_WhenFollowing_ThenTheApiReturnsBadRequest()
    {
        var other = await RegisterUserAsync("Blocked follow", "blocked-follow@example.com");
        await _client.PostAsync($"/api/users/{other.UserId}/block", null);

        var response = await _client.PostAsync($"/api/users/{other.UserId}/follow", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [TestCase("block")]
    [TestCase("follow")]
    public async Task GivenYourself_WhenCreatingARelationship_ThenTheApiReturnsBadRequest(string relationship)
    {
        var response = await _client.PostAsync($"/api/users/1/{relationship}", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GivenYourself_WhenUnblocking_ThenTheApiReturnsBadRequest()
    {
        var response = await _client.DeleteAsync("/api/users/1/block");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GivenAMissingUser_WhenBlockingOrUnblocking_ThenTheApiReturnsNotFound()
    {
        var block = await _client.PostAsync("/api/users/999/block", null);
        var unblock = await _client.DeleteAsync("/api/users/999/block");

        Assert.Multiple(() =>
        {
            Assert.That(block.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(unblock.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        });
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
        Assert.That(updated?.Email, Is.EqualTo("test@example.com"));
    }

    [Test]
    public async Task GivenAnotherUsersProfile_WhenUpdatingIt_ThenTheApiReturnsNotFound()
    {
        var other = await RegisterUserAsync("Other", "notmine@example.com");

        var response = await _client.PutAsJsonAsync($"/api/users/{other.UserId}", new
        {
            displayName = "Hijacked"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GivenTheCorrectPassword_WhenChangingEmail_ThenTheNewEmailCanBeUsedToLogIn()
    {
        var user = await RegisterUserAsync("Email User", "old-email@example.com");
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(user.UserId));

        var response = await _client.PutAsJsonAsync("/api/users/me/email", new
        {
            email = " New-Email@Example.com ",
            currentPassword = "Password123!"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var updated = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.That(updated?.Email, Is.EqualTo("new-email@example.com"));
        var login = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "new-email@example.com",
            password = "Password123!"
        });
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task GivenTheWrongPassword_WhenChangingEmail_ThenTheEmailIsNotChanged()
    {
        var user = await RegisterUserAsync("Email User", "unchanged@example.com");
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(user.UserId));

        var response = await _client.PutAsJsonAsync("/api/users/me/email", new
        {
            email = "attacker@example.com",
            currentPassword = "WrongPassword!"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var profile = await (await _client.GetAsync("/api/users/me")).Content.ReadFromJsonAsync<UserResponse>();
        Assert.That(profile?.Email, Is.EqualTo("unchanged@example.com"));
    }

    [Test]
    public async Task GivenTheCorrectPassword_WhenChangingPassword_ThenOnlyTheNewPasswordCanBeUsedToLogIn()
    {
        var user = await RegisterUserAsync("Password User", "password-user@example.com");
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(user.UserId));

        var response = await _client.PutAsJsonAsync("/api/users/me/password", new
        {
            currentPassword = "Password123!",
            newPassword = "UpdatedPassword456!"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var oldLogin = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "password-user@example.com",
            password = "Password123!"
        });
        var newLogin = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "password-user@example.com",
            password = "UpdatedPassword456!"
        });
        Assert.That(oldLogin.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(newLogin.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task GivenTheWrongPassword_WhenChangingPassword_ThenTheApiReturnsUnauthorized()
    {
        var user = await RegisterUserAsync("Password User", "wrong-password@example.com");
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(user.UserId));

        var response = await _client.PutAsJsonAsync("/api/users/me/password", new
        {
            currentPassword = "WrongPassword!",
            newPassword = "UpdatedPassword456!"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task GivenYourOwnAccount_WhenDeletingIt_ThenTheApiReturnsNoContent()
    {
        await RegisterUserAsync("Bootstrap administrator", "bootstrap-admin@example.com");
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

    // ----- Messages -----

    [Test]
    public async Task GivenNoMessagingRelationship_WhenSendingAMessage_ThenTheApiReturnsForbidden()
    {
        var recipient = await RegisterUserAsync("Message Recipient", "message-recipient@example.com");

        var response = await _client.PostAsJsonAsync($"/api/messages/{recipient.UserId}", new
        {
            body = "This should not be delivered."
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task GivenTheRecipientFollowsTheSender_WhenSendingAMessage_ThenTheApiCreatesIt()
    {
        var recipient = await RegisterUserAsync("Following Recipient", "following-recipient@example.com");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MyProjectDbContext>();
            db.Follows.Add(new Follow { FollowerId = recipient.UserId, FollowingId = 1 });
            await db.SaveChangesAsync();
        }

        var profile = await _client.GetFromJsonAsync<UserResponse>($"/api/users/{recipient.UserId}");
        var response = await _client.PostAsJsonAsync($"/api/messages/{recipient.UserId}", new
        {
            body = "Hello from the trail."
        });

        Assert.Multiple(() =>
        {
            Assert.That(profile?.CanMessage, Is.True);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        });
    }

    [Test]
    public async Task GivenAnExistingConversation_WhenSendingAMessage_ThenTheApiAllowsIt()
    {
        var recipient = await RegisterUserAsync("Existing Conversation", "existing-conversation@example.com");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MyProjectDbContext>();
            db.Messages.Add(new Message
            {
                SenderId = recipient.UserId,
                RecipientId = 1,
                Body = "A message to open the conversation."
            });
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync($"/api/messages/{recipient.UserId}", new
        {
            body = "Thanks for reaching out."
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
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
