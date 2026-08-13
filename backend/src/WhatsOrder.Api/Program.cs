using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using WhatsOrder.Api.Common;
using WhatsOrder.Api.Filters;
using WhatsOrder.Api.Hubs;
using WhatsOrder.Api.Middleware;
using WhatsOrder.Api.Realtime;
using WhatsOrder.Application;
using WhatsOrder.Application.Common;
using WhatsOrder.Application.Realtime;
using WhatsOrder.Infrastructure;
using WhatsOrder.Infrastructure.Common;
using WhatsOrder.Infrastructure.Identity;
using WhatsOrder.Infrastructure.Persistence;

// Ensure the web root exists before the builder captures it (uploads are served from here).
Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads"));

var builder = WebApplication.CreateBuilder(args);
var isTesting = builder.Environment.IsEnvironment("Testing");

builder.Host.UseSerilog((context, config) => config
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.PostConfigure<FileStorageOptions>(options =>
{
    if (string.IsNullOrEmpty(options.RootPath))
        options.RootPath = builder.Environment.WebRootPath
                           ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
});

// ── Authentication / authorization ────────────────────────────────────────
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
    throw new InvalidOperationException(
        "Jwt:SigningKey is not configured. Set the Jwt__SigningKey environment variable (64+ random characters).");
if (jwtOptions.SigningKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters long.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
        // SignalR cannot send an Authorization header over WebSockets — the browser
        // client passes the JWT as ?access_token=… on hub requests only.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken)
                    && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// ── Real-time (SignalR) ───────────────────────────────────────────────────
// String enums so hub payloads match the REST API's JSON shape exactly.
builder.Services
    .AddSignalR()
    .AddJsonProtocol(options =>
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<IOrderEventBroadcaster, SignalROrderEventBroadcaster>();

// ── Controllers, validation, JSON ─────────────────────────────────────────
builder.Services
    .AddControllers(options => options.Filters.Add<FluentValidationFilter>())
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// ── CORS ──────────────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials())); // SignalR negotiate requires credentials support

// ── Rate limiting (per client IP; disabled under integration tests) ───────
if (!isTesting)
{
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("auth", context => FixedWindowByIp(context, permitLimit: 10));
        options.AddPolicy("public", context => FixedWindowByIp(context, permitLimit: 120));
        options.AddPolicy("public-orders", context => FixedWindowByIp(context, permitLimit: 6));
    });
}

// ── Swagger ───────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "WhatsOrder Oman API",
        Version = "v1",
        Description = "Storefront + order management API for small businesses in Oman."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste the access token from /api/auth/login."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            []
        }
    });
});

var app = builder.Build();

// ── Database migration + seeding (skipped under integration tests) ────────
if (!isTesting)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var useSqlite = (builder.Configuration["Database:Provider"] ?? "Postgres")
        .Equals("Sqlite", StringComparison.OrdinalIgnoreCase);

    var migrated = false;
    for (var attempt = 1; attempt <= 10 && !migrated; attempt++)
    {
        try
        {
            if (useSqlite)
                db.Database.EnsureCreated(); // migrations are PostgreSQL-specific
            else
                db.Database.Migrate();
            migrated = true;
        }
        catch (Exception ex) when (attempt < 10)
        {
            Log.Warning("Database not ready (attempt {Attempt}/10): {Message}. Retrying in 3s…", attempt, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    await DbSeeder.SeedRolesAsync(roleManager);

    var appOptions = builder.Configuration.GetSection(AppOptions.SectionName).Get<AppOptions>() ?? new AppOptions();
    if (appOptions.SeedDemoData)
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await DbSeeder.SeedDemoDataAsync(db, userManager, app.Logger);
    }
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "WhatsOrder API v1");
    options.DocumentTitle = "WhatsOrder Oman API";
});

app.UseStaticFiles();
app.UseRouting();
app.UseCors();

if (!isTesting)
    app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<OrderHub>("/hubs/orders");
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();

static RateLimitPartition<string> FixedWindowByIp(HttpContext context, int permitLimit) =>
    RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });

/// <summary>Exposes Program to WebApplicationFactory in integration tests.</summary>
public partial class Program;
