using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WhatsOrder.Application.WhatsApp;
using WhatsOrder.Infrastructure.Persistence;
using Xunit;

namespace WhatsOrder.IntegrationTests;

/// <summary>Captures every outbound WhatsApp call instead of hitting Meta.</summary>
public class FakeWhatsAppApiClient : IWhatsAppApiClient
{
    public sealed record SentMessage(string To, string Body);

    private readonly List<SentMessage> _messages = [];
    private readonly object _lock = new();

    public IReadOnlyList<SentMessage> Messages
    {
        get { lock (_lock) return _messages.ToList(); }
    }

    public bool IsConfigured => true;

    public Task<WhatsAppSendResult> SendTextAsync(string toE164, string body, CancellationToken ct = default)
    {
        lock (_lock) _messages.Add(new SentMessage(toE164, body));
        return Task.FromResult(WhatsAppSendResult.Ok($"wamid.test.{Guid.NewGuid():N}"));
    }

    public Task<WhatsAppSendResult> SendTemplateAsync(
        string toE164, string templateName, string languageCode,
        IReadOnlyList<string> bodyParameters, CancellationToken ct = default)
    {
        lock (_lock) _messages.Add(new SentMessage(toE164, string.Join("\n", bodyParameters)));
        return Task.FromResult(WhatsAppSendResult.Ok($"wamid.test.{Guid.NewGuid():N}"));
    }
}

public class TestAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // A file-based SQLite database: unlike a single shared in-memory connection, every
    // DbContext scope (request threads AND the background WhatsApp dispatcher) gets its
    // own connection, and SQLite's file locking serializes concurrent writers safely.
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(), $"whatsorder-test-{Guid.NewGuid():N}.db");

    public const string WebhookVerifyToken = "test-verify-token";
    public const string WebhookAppSecret = "test-app-secret";

    public FakeWhatsAppApiClient WhatsAppClient { get; } = new();

    public TestAppFactory()
    {
        // Plain environment variables: present in configuration from the very first read,
        // including values Program.cs consumes inline during startup.
        Environment.SetEnvironmentVariable("Jwt__SigningKey",
            "integration-test-signing-key-0123456789abcdef0123456789abcdef");
        Environment.SetEnvironmentVariable("Jwt__AccessTokenMinutes", "60");
        Environment.SetEnvironmentVariable("App__SeedDemoData", "false");
        Environment.SetEnvironmentVariable("WhatsApp__Enabled", "true");
        Environment.SetEnvironmentVariable("WhatsApp__PhoneNumberId", "test");
        Environment.SetEnvironmentVariable("WhatsApp__AccessToken", "test");
        Environment.SetEnvironmentVariable("WhatsApp__WebhookVerifyToken", WebhookVerifyToken);
        Environment.SetEnvironmentVariable("WhatsApp__AppSecret", WebhookAppSecret);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite($"Data Source={_dbPath}"));

            services.RemoveAll<IWhatsAppApiClient>();
            services.AddSingleton<IWhatsAppApiClient>(WhatsAppClient);
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        await DbSeeder.SeedRolesAsync(roleManager);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        SqliteConnection.ClearAllPools();
        try
        {
            File.Delete(_dbPath);
        }
        catch (IOException)
        {
            // Best effort — temp files are cleaned by the OS eventually.
        }
    }

    /// <summary>Run assertions/arrangement directly against the test database.</summary>
    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>Polls until the background WhatsApp dispatcher has produced the expected state.</summary>
    public async Task<T> WaitForAsync<T>(Func<Task<T?>> probe, TimeSpan? timeout = null) where T : class
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (DateTime.UtcNow < deadline)
        {
            if (await probe() is { } result)
                return result;
            await Task.Delay(50);
        }
        throw new TimeoutException("Condition was not met in time.");
    }
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<TestAppFactory>;
