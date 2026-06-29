using System.Text;
using AssetMgmt.Application.Allocations;
using AssetMgmt.Application.Assets;
using AssetMgmt.Application.Auth;
using AssetMgmt.Application.Departments;
using AssetMgmt.Application.Requests;
using AssetMgmt.Application.Users;
using AssetMgmt.Infrastructure.Persistence;
using AssetMgmt.Infrastructure.Services;
using AssetMgmt.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(BuildConnectionString(builder.Configuration)));

// --- Auth configuration ---
var jwtOptions = new JwtOptions();
builder.Configuration.GetSection(JwtOptions.SectionName).Bind(jwtOptions);
jwtOptions.Secret = builder.Configuration["JWT_SECRET"]
    ?? throw new InvalidOperationException("JWT_SECRET is not set (.env).");
builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(jwtOptions));

builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<AuthService>();

// --- Feature services (Days 3-5) ---
builder.Services.AddScoped<IQrCodeService, QrCodeService>();
builder.Services.AddScoped<AssetModelService>();
builder.Services.AddScoped<AssetInstanceService>();
builder.Services.AddScoped<AllocationRequestService>();
builder.Services.AddScoped<AllocationHistoryService>();
builder.Services.AddScoped<AssetLifecycleService>();
builder.Services.AddScoped<UserAdminService>();
builder.Services.AddScoped<DepartmentService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminIT", p => p.RequireRole("AdminIT"));
    options.AddPolicy("RequireManager", p => p.RequireRole("Manager", "AdminIT"));
    options.AddPolicy("RequireEmployee", p => p.RequireRole("Employee", "Manager", "AdminIT"));
});

// OpenAPI with Bearer auth support for Scalar.
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter the JWT access token (without the 'Bearer ' prefix)."
    });
    options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", null, null),
            new List<string>()
        }
    });
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseStaticFiles(); // serve generated QR codes from wwwroot/qr

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Emit the OpenAPI document at /openapi/{documentName}.json for Scalar to consume.
    app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
    app.MapScalarApiReference(); // UI at /scalar/v1
}

app.UseAuthentication();
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
