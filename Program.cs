using AssetMgmt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(BuildConnectionString(builder.Configuration)));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Emit the OpenAPI document at /openapi/{documentName}.json for Scalar to consume.
    app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
    app.MapScalarApiReference(); // UI at /scalar/v1
}

app.UseAuthorization();

app.MapControllers();

app.Run();

static string BuildConnectionString(IConfiguration config)
{
    var server = config["DB_SERVER"] ?? "localhost";
    var port = config["DB_PORT"] ?? "1433";
    var database = config["DB_NAME"] ?? "AssetMgmt";
    var user = config["DB_USER"] ?? throw new InvalidOperationException("DB_USER is not set (.env).");
    var password = config["DB_PASSWORD"] ?? throw new InvalidOperationException("DB_PASSWORD is not set (.env).");
    var trustCert = config["DB_TRUST_CERT"] ?? "True";

    return $"Server={server},{port};Database={database};User Id={user};Password={password};"
         + $"TrustServerCertificate={trustCert};MultipleActiveResultSets=True;Encrypt=True";
}
