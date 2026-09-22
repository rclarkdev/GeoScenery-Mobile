using System.Text;
using GeoScenery.Api.Auth;
using GeoScenery.Api.Endpoints;
using GeoScenery.Data.Context;
using GeoScenery.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

builder.Services.AddDbContext<MyProjectDbContext>(options => options.UseSqlServer(connectionString));
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");
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
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ISceneService, SceneService>();
builder.Services.AddScoped<IVisitService, VisitService>();
builder.Services.AddScoped<IFollowService, FollowService>();
builder.Services.AddValidation();
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientApp", policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("ClientApp");
app.UseAuthentication();
app.UseAuthorization();
app.MapAuthEndpoints(builder.Configuration);
app.MapGeoSceneryEndpoints();

app.Run();

public partial class Program;
