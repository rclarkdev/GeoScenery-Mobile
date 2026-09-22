using System.Text;
using System.Threading.RateLimiting;
using GeoScenery.Api.Auth;
using GeoScenery.Api.Endpoints;
using GeoScenery.Data.Context;
using GeoScenery.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
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
    options.AddFixedWindowLimiter("password-reset", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(15);
        limiterOptions.QueueLimit = 0;
    });
});
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ISceneService, SceneService>();
builder.Services.AddScoped<IVisitService, VisitService>();
builder.Services.AddScoped<IFollowService, FollowService>();
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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("ClientApp");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapAuthEndpoints(builder.Configuration);
app.MapGeoSceneryEndpoints();

app.Run();

public partial class Program;
