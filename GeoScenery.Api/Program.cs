using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using GeoScenery.Api.Auth;
using GeoScenery.Api.Endpoints;
using GeoScenery.Api.Logging;
using GeoScenery.Api.Middleware;
using GeoScenery.Api.Storage;
using GeoScenery.Data.Context;
using GeoScenery.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

builder.Services.AddDbContext<MyProjectDbContext>(options => options.UseSqlServer(connectionString));
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
{
    if (builder.Environment.IsProduction())
    {
        throw new InvalidOperationException("Jwt:Key must be configured outside Development and Testing.");
    }

    jwtKey = "development-only-change-this-key-before-deployment-geoscenery";
}
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "GeoScenery";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "GeoScenery.Client";
builder.Services.AddSingleton<IPasswordHasher<GeoScenery.Data.Models.User>, PasswordHasher<GeoScenery.Data.Models.User>>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ILoginAttemptTracker, LoginAttemptTracker>();

// Image upload storage + processing configuration. Files are stored on the local
// filesystem and served from /uploads; production deployments should point
// Storage:Local:RootPath at a persistent directory (or replace IFileStorageService
// with an object-storage implementation).
var storageRoot = StoragePaths.ResolveRootPath(builder.Configuration);
Directory.CreateDirectory(storageRoot);
builder.Services.AddSingleton<IFileStorageService>(new LocalFileStorageService(storageRoot));
builder.Services.Configure<ImageUploadOptions>(builder.Configuration.GetSection("Storage"));
var uploadOptions = new ImageUploadOptions();
builder.Configuration.GetSection("Storage").Bind(uploadOptions);
builder.Services.Configure<FormOptions>(formOptions =>
    formOptions.MultipartBodyLengthLimit = uploadOptions.MaxUploadBytes + 65_536);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    // Password-reset budget: partitioned so one abusive client cannot exhaust the
    // budget for every other user. Anonymous callers share a bucket with their
    // client IP; authenticated callers get a bucket keyed to their user id.
    options.AddPolicy("password-reset", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetRateLimitPartitionKey(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0
            }));

    // Login brute-force protection: each client IP gets its own budget so one
    // source cannot guess credentials without limit, and noisy callers cannot
    // starve other users of a shared pool.
    options.AddPolicy("auth-login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0
            }));

    // Account-creation abuse protection: a handful of registrations per client IP
    // in a window is enough for legitimate users (including retries) while
    // limiting automated account creation.
    options.AddPolicy("auth-register", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0
            }));

    // 429 Too Many Requests is the correct status for an exhausted policy
    // (the framework default of 503 is a poor fit for client abuse).
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = retryAfter.TotalSeconds.ToString("0");
        }

        return ValueTask.CompletedTask;
    };
});
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ISceneService, SceneService>();
builder.Services.AddScoped<IVisitService, VisitService>();
builder.Services.AddScoped<IFollowService, FollowService>();
builder.Services.AddScoped<ISqlAuditLog, SqlAuditLog>();
builder.Services.AddValidation();
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientApp", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        }
        else if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            throw new InvalidOperationException("Cors:AllowedOrigins must be configured outside Development and Testing.");
        }
    });
});

var app = builder.Build();

// Forwarded headers handling is opt-in (ForwardedHeaders:Enabled) because it must
// only be trusted when the ingress (e.g. Azure App Service / a load balancer) is
// known to strip or set X-Forwarded-* correctly. When enabled, HttpContext's
// RemoteIpAddress reflects the real client IP and the rate-limit partitions above
// apply per real client. Unconditionally trusting arbitrary forwarded headers
// would be a spoofing risk, so the default is off.
if (builder.Configuration.GetValue("ForwardedHeaders:Enabled", false))
{
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
            | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
    });
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("ClientApp");
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(storageRoot),
    RequestPath = "/uploads"
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<SqlRequestLoggingMiddleware>();
app.MapHealthEndpoints();
app.MapAuthEndpoints(builder.Configuration);
app.MapGeoSceneryEndpoints();
app.MapImageEndpoints();

app.Run();

// Partition key for the password-reset limiter: authenticated callers are keyed
// by user id (stable across IP changes), anonymous callers by client IP.
static string GetRateLimitPartitionKey(HttpContext context)
{
    var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
    return !string.IsNullOrEmpty(userId) ? $"user:{userId}" : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
}

public partial class Program;
