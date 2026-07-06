using System.Text;
using AssetMgmt.Application.Allocations;
using AssetMgmt.Application.Agents;
using AssetMgmt.Application.Assets;
using AssetMgmt.Application.Auth;
using AssetMgmt.Application.Departments;
using AssetMgmt.Application.Handover;
using AssetMgmt.Application.Reports;
using AssetMgmt.Application.Requests;
using AssetMgmt.Application.Users;
using AssetMgmt.Infrastructure.Jobs;
using AssetMgmt.Infrastructure.Persistence;
using AssetMgmt.Infrastructure.Services;
using AssetMgmt.Middleware;
using Hangfire;
using Hangfire.SqlServer;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using QuestPDF.Infrastructure;
using Scalar.AspNetCore;

// QuestPDF Community license (free for orgs under $1M revenue). Required before rendering.
QuestPDF.Settings.License = LicenseType.Community;

DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// Add services to the container.
builder.Services.AddControllers();

// FluentValidation (Day 9) — auto-validate all request DTOs.
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<AiRouterOptions>(builder.Configuration.GetSection(AiRouterOptions.SectionName));

// DEV: default allows any origin. TODO: switch back to "http://localhost:4200"
// (or the real frontend origin) before production.
var corsOrigins = (builder.Configuration["CORS_ALLOWED_ORIGINS"] ?? "*")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        // DEV: "*" allows any origin. Switch back to a fixed origin list for production.
        if (corsOrigins.Contains("*"))
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        else
            policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod();
    });
});

var connectionString = BuildConnectionString(builder.Configuration);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

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
builder.Services.AddScoped<AiAssetAccessService>();
builder.Services.AddScoped<AiConversationStore>();
builder.Services.AddScoped<AiOperationsService>();
builder.Services.AddScoped<AiAskService>();
builder.Services.AddScoped<IAiRouterService, OpenAiRouterService>();
builder.Services.AddScoped<IAiToolHandler, GetMyAssetsTool>();
builder.Services.AddScoped<IAiToolHandler, GetAssetStatusTool>();
builder.Services.AddScoped<IAiToolHandler, SearchManualSourcesTool>();
builder.Services.AddScoped<IAiToolHandler, CreateMaintenanceDraftTool>();
builder.Services.AddScoped<IAiToolHandler, ListAssetsTool>();
builder.Services.AddScoped<IAiToolHandler, ListAssetModelsTool>();
builder.Services.AddScoped<IAiToolHandler, CreateAllocationRequestTool>();
builder.Services.AddScoped<IAiToolHandler, ListPendingRequestsTool>();
builder.Services.AddScoped<IAiToolHandler, ApproveAllocationRequestTool>();
builder.Services.AddScoped<IAiToolHandler, RejectAllocationRequestTool>();
builder.Services.AddScoped<IAiToolHandler, AskClarifyingQuestionTool>();

// --- Day 8: PDF handover + reports ---
builder.Services.AddScoped<IHandoverDocumentService, HandoverDocumentService>();
builder.Services.AddScoped<ReportService>();

// --- Startup seeding ---
builder.Services.AddScoped<DbSeeder>();

// --- Background jobs (Day 7) ---
builder.Services.AddScoped<LockTimeoutJob>();
builder.Services.AddScoped<DepreciationJob>();

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
    {
        SchemaName = "HangFire",
        PrepareSchemaIfNecessary = true,
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true
    }));
builder.Services.AddHangfireServer();

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

// Activate seeded demo accounts (replace placeholder password hashes).
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
    await seeder.SeedPasswordsAsync();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseStaticFiles(); // serve generated QR codes from wwwroot/qr

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Emit the OpenAPI document at /openapi/{documentName}.json for Scalar to consume.
    app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
    app.MapScalarApiReference(); // UI at /scalar/v1
}

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

// Audit sensitive (mutating) API calls — after auth so the user is known.
app.UseMiddleware<AuditLoggingMiddleware>();

// Hangfire dashboard + recurring jobs (Day 7).
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[]
    {
        new HangfireDashboardAuthorizationFilter(app.Environment.IsDevelopment())
    }
});

RecurringJob.AddOrUpdate<LockTimeoutJob>(
    "lock-timeout",
    job => job.RunAsync(CancellationToken.None),
    "*/5 * * * *"); // every 5 minutes

RecurringJob.AddOrUpdate<DepreciationJob>(
    "depreciation-monthly",
    job => job.RunAsync(CancellationToken.None),
    "0 1 1 * *"); // 01:00 UTC on the 1st of each month

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
