using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MiniTube.Domain.Entities;

namespace MiniTube.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IDataProtectionKeyContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Video> Videos => Set<Video>();
    public DbSet<VideoRendition> VideoRenditions => Set<VideoRendition>();
    public DbSet<VideoReaction> VideoReactions => Set<VideoReaction>();
    public DbSet<WatchHistoryEntry> WatchHistory => Set<WatchHistoryEntry>();
    public DbSet<VideoView> VideoViews => Set<VideoView>();
    public DbSet<Playlist> Playlists => Set<Playlist>();
    public DbSet<PlaylistItem> PlaylistItems => Set<PlaylistItem>();

    /// <summary>Keys that protect email-confirmation / password-reset tokens. Stored in DB so they survive restarts.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Friendlier table names
        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<ApplicationRole>().ToTable("Roles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>().ToTable("UserTokens");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<Guid>>().ToTable("RoleClaims");

        builder.Entity<ApplicationUser>(e =>
        {
            e.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Bio).HasMaxLength(1000);
            e.Property(x => x.AvatarUrl).HasMaxLength(500);
        });

        builder.Entity<Video>(e =>
        {
            e.ToTable("Videos");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(5000);
            e.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.OriginalFileName).HasMaxLength(260);
            e.Property(x => x.SourcePath).HasMaxLength(500);
            e.Property(x => x.ThumbnailUrl).HasMaxLength(500);
            e.Property(x => x.ProcessingError).HasMaxLength(1000);

            e.HasIndex(x => x.Status);
            e.HasIndex(x => new { x.OwnerId, x.CreatedAt });
            e.HasIndex(x => new { x.Visibility, x.Status, x.PublishedAt });

            // Deleting a user must be an explicit decision (files on disk need cleaning up too)
            e.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<VideoRendition>(e =>
        {
            e.ToTable("VideoRenditions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(20).IsRequired();
            e.HasIndex(x => new { x.VideoId, x.Name }).IsUnique();
            e.HasOne(x => x.Video).WithMany(v => v.Renditions).HasForeignKey(x => x.VideoId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<VideoReaction>(e =>
        {
            e.ToTable("VideoReactions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(10);
            e.HasIndex(x => new { x.UserId, x.VideoId }).IsUnique();
            e.HasIndex(x => new { x.UserId, x.Type, x.CreatedAt });
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Video).WithMany().HasForeignKey(x => x.VideoId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<WatchHistoryEntry>(e =>
        {
            e.ToTable("WatchHistory");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.UserId, x.VideoId }).IsUnique();
            e.HasIndex(x => new { x.UserId, x.LastWatchedAt });
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Video).WithMany().HasForeignKey(x => x.VideoId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<VideoView>(e =>
        {
            e.ToTable("VideoViews");
            e.HasKey(x => x.Id);
            e.Property(x => x.ViewerKey).HasMaxLength(64);
            e.HasIndex(x => new { x.VideoId, x.UserId, x.CreatedAt });
            e.HasIndex(x => new { x.VideoId, x.ViewerKey, x.CreatedAt });
            // UserId is deliberately a plain column (no FK): anonymous rows have none
            e.HasOne(x => x.Video).WithMany().HasForeignKey(x => x.VideoId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Playlist>(e =>
        {
            e.ToTable("Playlists");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(150).IsRequired();
            e.Property(x => x.Description).HasMaxLength(5000);
            e.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);

            e.HasIndex(x => new { x.OwnerId, x.UpdatedAt });
            e.HasIndex(x => new { x.Visibility, x.Kind, x.UpdatedAt });

            // At most ONE "Watch later" playlist per user
            e.HasIndex(x => x.OwnerId)
                .IsUnique()
                .HasFilter("[Kind] = 'WatchLater'")
                .HasDatabaseName("IX_Playlists_OwnerId_WatchLater");

            e.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PlaylistItem>(e =>
        {
            e.ToTable("PlaylistItems");
            e.HasKey(x => x.Id);

            // Deliberately NOT unique: a move shifts a range of positions in one UPDATE
            e.HasIndex(x => new { x.PlaylistId, x.Position });
            e.HasIndex(x => new { x.PlaylistId, x.VideoId }).IsUnique(); // a video appears once per playlist
            e.HasIndex(x => x.VideoId);

            e.HasOne(x => x.Playlist).WithMany(p => p.Items).HasForeignKey(x => x.PlaylistId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Video).WithMany().HasForeignKey(x => x.VideoId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RefreshToken>(e =>
        {
            e.ToTable("RefreshTokens");
            e.HasKey(x => x.Id);
            e.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            e.Property(x => x.ReplacedByTokenHash).HasMaxLength(128);
            e.Property(x => x.CreatedByIp).HasMaxLength(64);
            e.Property(x => x.RevokedByIp).HasMaxLength(64);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => new { x.UserId, x.RevokedAt });
            e.HasOne(x => x.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
