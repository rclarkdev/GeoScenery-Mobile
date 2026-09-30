using GeoScenery.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace GeoScenery.Data.Context;

public class MyProjectDbContext : DbContext
{
    public MyProjectDbContext(DbContextOptions<MyProjectDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Scene> Scenes => Set<Scene>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<Follow> Follows => Set<Follow>();
    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<SceneTag> SceneTags => Set<SceneTag>();
    public DbSet<SceneRating> SceneRatings => Set<SceneRating>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<AppLogEntry> AppLogEntries => Set<AppLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasData(new User
        {
            Id = 1,
            DisplayName = "Master",
            Email = "vjryanaye@gmail.com",
            PasswordHash = "!",
            CreatedAt = new DateTimeOffset(2026, 9, 22, 0, 0, 0, TimeSpan.Zero)
        });

        modelBuilder.Entity<User>()
            .HasIndex(user => user.Email)
            .IsUnique();

        modelBuilder.Entity<PasswordResetToken>()
            .HasIndex(token => token.TokenHash)
            .IsUnique();

        modelBuilder.Entity<PasswordResetToken>()
            .HasOne(token => token.User)
            .WithMany(user => user.PasswordResetTokens)
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AppLogEntry>()
            .HasIndex(log => log.CreatedAt);

        modelBuilder.Entity<AppLogEntry>()
            .HasIndex(log => log.CorrelationId);

        modelBuilder.Entity<AppLogEntry>()
            .HasIndex(log => log.UserId);

        modelBuilder.Entity<Scene>()
            .HasOne(scene => scene.OwnerUser)
            .WithMany(user => user.Scenes)
            .HasForeignKey(scene => scene.OwnerUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<SceneTag>()
            .HasIndex(sceneTag => new { sceneTag.SceneId, sceneTag.Tag })
            .IsUnique();

        modelBuilder.Entity<SceneTag>()
            .HasOne(sceneTag => sceneTag.Scene)
            .WithMany(scene => scene.Tags)
            .HasForeignKey(sceneTag => sceneTag.SceneId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SceneRating>()
            .HasIndex(sceneRating => new { sceneRating.SceneId, sceneRating.UserId })
            .IsUnique();

        modelBuilder.Entity<SceneRating>()
            .HasOne(sceneRating => sceneRating.Scene)
            .WithMany(scene => scene.Ratings)
            .HasForeignKey(sceneRating => sceneRating.SceneId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SceneRating>()
            .HasOne(sceneRating => sceneRating.User)
            .WithMany()
            .HasForeignKey(sceneRating => sceneRating.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Visit>()
            .HasIndex(visit => new { visit.UserId, visit.SceneId })
            .IsUnique();

        modelBuilder.Entity<Visit>()
            .HasOne(visit => visit.Scene)
            .WithMany(scene => scene.Visits)
            .HasForeignKey(visit => visit.SceneId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Visit>()
            .HasOne(visit => visit.User)
            .WithMany(user => user.Visits)
            .HasForeignKey(visit => visit.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Follow>()
            .HasIndex(follow => new { follow.FollowerId, follow.FollowingId })
            .IsUnique();

        modelBuilder.Entity<Follow>()
            .HasOne(follow => follow.Follower)
            .WithMany(user => user.Following)
            .HasForeignKey(follow => follow.FollowerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Follow>()
            .HasOne(follow => follow.Following)
            .WithMany(user => user.Followers)
            .HasForeignKey(follow => follow.FollowingId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<UserBlock>()
            .HasIndex(block => new { block.BlockerId, block.BlockedId })
            .IsUnique();

        modelBuilder.Entity<UserBlock>()
            .HasOne(block => block.Blocker)
            .WithMany(user => user.Blocking)
            .HasForeignKey(block => block.BlockerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<UserBlock>()
            .HasOne(block => block.Blocked)
            .WithMany(user => user.BlockedBy)
            .HasForeignKey(block => block.BlockedId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Message>()
            .HasOne(message => message.Sender)
            .WithMany()
            .HasForeignKey(message => message.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Message>()
            .HasOne(message => message.Recipient)
            .WithMany()
            .HasForeignKey(message => message.RecipientId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Message>()
            .HasIndex(message => new { message.SenderId, message.RecipientId, message.CreatedAt });
    }
}
