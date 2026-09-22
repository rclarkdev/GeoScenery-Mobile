using GeoScenery.Api.Endpoints;
using GeoScenery.Data.Context;
using GeoScenery.Data.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

builder.Services.AddDbContext<MyProjectDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ISceneService, SceneService>();
builder.Services.AddScoped<IVisitService, VisitService>();
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
app.MapGeoSceneryEndpoints();

app.Run();

public partial class Program;
