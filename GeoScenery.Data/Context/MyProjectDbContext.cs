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
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<AppLogEntry> AppLogEntries => Set<AppLogEntry>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<ContentReport> ContentReports => Set<ContentReport>();
    public DbSet<AdminActionAudit> AdminActionAudits => Set<AdminActionAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>().HasData(
            new Role { Name = AppRoles.Member },
            new Role { Name = AppRoles.Admin });

        modelBuilder.Entity<User>()
            .Property(user => user.IsEmailVerified)
            .HasDefaultValue(true);
        modelBuilder.Entity<User>()
            .Property(user => user.IsSuspended)
            .HasDefaultValue(false);
        modelBuilder.Entity<Scene>()
            .Property(scene => scene.IsHidden)
            .HasDefaultValue(false);
        modelBuilder.Entity<Scene>()
            .Property(scene => scene.IsPublic)
            .HasDefaultValue(true);

        modelBuilder.Entity<UserRole>()
            .HasKey(userRole => new { userRole.UserId, userRole.RoleName });

        modelBuilder.Entity<UserRole>()
            .HasOne(userRole => userRole.User)
            .WithMany(user => user.Roles)
            .HasForeignKey(userRole => userRole.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserRole>()
            .HasOne(userRole => userRole.Role)
            .WithMany(role => role.UserRoles)
            .HasForeignKey(userRole => userRole.RoleName)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ContentReport>()
            .ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_ContentReports_TargetType",
                    "TargetType IN ('Profile', 'Scene')");
                table.HasCheckConstraint(
                    "CK_ContentReports_Status",
                    "Status IN ('Pending', 'Reviewed', 'Dismissed', 'Actioned')");
            });

        modelBuilder.Entity<ContentReport>()
            .Property(report => report.Status)
            .HasDefaultValue(ContentReportStatuses.Pending);

        modelBuilder.Entity<ContentReport>()
            .HasOne(report => report.Reporter)
            .WithMany()
            .HasForeignKey(report => report.ReporterId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ContentReport>()
            .HasIndex(report => new { report.TargetType, report.TargetId });

        modelBuilder.Entity<ContentReport>()
            .HasIndex(report => report.CreatedAt);

        modelBuilder.Entity<ContentReport>()
            .HasIndex(report => new { report.Status, report.Id });

        modelBuilder.Entity<ContentReport>()
            .HasIndex(report => new { report.ReporterId, report.TargetType, report.TargetId })
            .IsUnique()
            .HasFilter("[Status] IN ('Pending', 'Reviewed')");

        modelBuilder.Entity<ContentReport>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(report => report.ReviewedByUserId)
            // SQL Server rejects two SET NULL paths from Users to ContentReports
            // (ReporterId and ReviewedByUserId). UserService clears reviewer ids
            // in the same transaction before deleting an account.
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<AdminActionAudit>()
            .HasIndex(audit => audit.CreatedAt);

        modelBuilder.Entity<AdminActionAudit>()
            .HasIndex(audit => new { audit.TargetType, audit.TargetId });

        modelBuilder.Entity<AdminActionAudit>()
            .HasIndex(audit => audit.ActorUserId);

        modelBuilder.Entity<User>()
            .HasIndex(user => user.Email)
            .IsUnique();

        modelBuilder.Entity<PasswordResetToken>()
            .HasIndex(token => token.TokenHash)
            .IsUnique();

        modelBuilder.Entity<EmailVerificationToken>()
            .HasIndex(token => token.TokenHash)
            .IsUnique();

        modelBuilder.Entity<EmailVerificationToken>()
            .HasOne(token => token.User)
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);

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
