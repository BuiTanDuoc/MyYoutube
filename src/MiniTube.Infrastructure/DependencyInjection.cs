using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniTube.Application.Auth;
using MiniTube.Application.Common;
using MiniTube.Application.Playback;
using MiniTube.Application.Playlists;
using MiniTube.Application.Search;
using MiniTube.Application.Users;
using MiniTube.Application.Videos;
using MiniTube.Domain.Entities;
using MiniTube.Infrastructure.Options;
using MiniTube.Infrastructure.Persistence;
using MiniTube.Infrastructure.Playlists;
using MiniTube.Infrastructure.Search;
using MiniTube.Infrastructure.Services;
using MiniTube.Infrastructure.Storage;
using MiniTube.Infrastructure.Videos;

namespace MiniTube.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseSqlServer(config.GetConnectionString("DefaultConnection"),
                sql => sql.EnableRetryOnFailure()));

        services.AddOptions<JwtOptions>()
            .Bind(config.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.Configure<EmailOptions>(config.GetSection("Email"));
        services.Configure<FrontendOptions>(config.GetSection("Frontend"));
        services.Configure<StorageOptions>(config.GetSection("Storage"));
        services.Configure<VideoOptions>(config.GetSection("Video"));
        services.Configure<SearchOptions>(config.GetSection("Search"));

        // Email-confirmation / reset-password tokens are protected with these keys.
        // Persisting them in the DB keeps old links valid across restarts and multiple instances.
        services.AddDataProtection()
            .SetApplicationName("MiniTube")
            .PersistKeysToDbContext<AppDbContext>();

        services.AddIdentityCore<ApplicationUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.User.AllowedUserNameCharacters =
                    "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_.";

                o.Password.RequiredLength = 8;
                o.Password.RequireDigit = true;
                o.Password.RequireLowercase = true;
                o.Password.RequireUppercase = true;
                o.Password.RequireNonAlphanumeric = false;

                o.Lockout.AllowedForNewUsers = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        // Confirmation + reset links live for 2 hours
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(2));

        // Real SMTP when Email:Host is set, otherwise log the email (handy in dev)
        if (!string.IsNullOrWhiteSpace(config["Email:Host"]))
            services.AddScoped<IAppEmailSender, SmtpEmailSender>();
        else
            services.AddScoped<IAppEmailSender, LogEmailSender>();

        services.AddSingleton<IFileStorage, LocalFileStorage>();

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();

        // ---- Videos ----
        services.AddSingleton<StoragePaths>();
        services.AddSingleton<FfmpegTools>();
        services.AddSingleton<VideoProcessingQueue>();
        services.AddSingleton<IVideoProcessingQueue>(sp => sp.GetRequiredService<VideoProcessingQueue>());
        services.AddScoped<IVideoProcessor, VideoProcessor>();
        services.AddScoped<IVideoService, VideoService>();
        services.AddScoped<IVideoStreamService, VideoStreamService>();
        services.AddScoped<IPlaybackService, PlaybackService>();
        services.AddScoped<IPlaylistService, PlaylistService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<ISearchIndexer, SearchIndexer>();
        services.AddHostedService<SearchBackfillService>();
        services.AddHostedService<VideoProcessingWorker>();

        services.AddHostedService<RefreshTokenCleanupService>();

        return services;
    }
}
