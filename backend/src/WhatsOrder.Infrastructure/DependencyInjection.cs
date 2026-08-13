using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WhatsOrder.Application.Account;
using WhatsOrder.Application.Auth;
using WhatsOrder.Application.Common;
using WhatsOrder.Application.WhatsApp;
using WhatsOrder.Infrastructure.Common;
using WhatsOrder.Infrastructure.Email;
using WhatsOrder.Infrastructure.Files;
using WhatsOrder.Infrastructure.Identity;
using WhatsOrder.Infrastructure.Persistence;
using WhatsOrder.Infrastructure.WhatsApp;

namespace WhatsOrder.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AppOptions>(configuration.GetSection(AppOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<WhatsAppOptions>(configuration.GetSection(WhatsAppOptions.SectionName));
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));

        // PostgreSQL is the production database. Database:Provider=Sqlite offers a
        // zero-dependency local mode (file database, schema via EnsureCreated).
        var databaseProvider = configuration["Database:Provider"] ?? "Postgres";
        services.AddDbContext<AppDbContext>(options =>
        {
            if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
                options.UseSqlite(configuration.GetConnectionString("Sqlite") ?? "Data Source=whatsorder.dev.db");
            else
                options.UseNpgsql(configuration.GetConnectionString("Default"));
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IBuyerProfileService, BuyerProfileService>();
        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddScoped<IEmailSender, LoggingEmailSender>();

        services.AddSingleton<WhatsAppNotificationQueue>();
        services.AddSingleton<IWhatsAppNotificationQueue>(sp => sp.GetRequiredService<WhatsAppNotificationQueue>());
        services.AddHttpClient<IWhatsAppApiClient, WhatsAppApiClient>(client =>
        {
            client.BaseAddress = new Uri("https://graph.facebook.com/");
            client.Timeout = TimeSpan.FromSeconds(20);
        });
        services.AddScoped<WhatsAppWebhookProcessor>();
        services.AddHostedService<WhatsAppDispatcher>();

        return services;
    }
}
