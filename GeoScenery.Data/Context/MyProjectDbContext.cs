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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(user => user.Email)
            .IsUnique();

        modelBuilder.Entity<Scene>()
            .HasOne(scene => scene.OwnerUser)
            .WithMany(user => user.Scenes)
            .HasForeignKey(scene => scene.OwnerUserId)
            .OnDelete(DeleteBehavior.SetNull);

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
    }
}
