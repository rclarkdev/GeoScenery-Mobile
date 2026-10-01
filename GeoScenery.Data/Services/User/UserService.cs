using GeoScenery.Data.Context;
using GeoScenery.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace GeoScenery.Data.Services;

public sealed class UserService : IUserService
{
    private readonly MyProjectDbContext _dbContext;

    public UserService(MyProjectDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users.AsNoTracking().OrderBy(user => user.DisplayName).ToListAsync(cancellationToken);
    }

    public Task<User?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public async Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        user.CreatedAt = DateTimeOffset.UtcNow;
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<User?> UpdateAsync(long id, User user, CancellationToken cancellationToken = default)
    {
        var existingUser = await _dbContext.Users.FindAsync([id], cancellationToken);
        if (existingUser is null)
        {
            return null;
        }

        var normalizedEmail = user.Email.Trim().ToLowerInvariant();
        if (await _dbContext.Users
                .AsNoTracking()
                .AnyAsync(candidate => candidate.Email == normalizedEmail && candidate.Id != id, cancellationToken))
        {
            throw new DuplicateEmailException();
        }

        existingUser.DisplayName = user.DisplayName;
        existingUser.Email = normalizedEmail;
        existingUser.ProfileImageUrl = user.ProfileImageUrl;
        existingUser.Latitude = user.Latitude;
        existingUser.Longitude = user.Longitude;
        existingUser.BirthDate = user.BirthDate;
        existingUser.Education = user.Education;
        existingUser.Hobbies = user.Hobbies;
        existingUser.Employment = user.Employment;
        existingUser.Bio = user.Bio;
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (UniqueConstraintGuard.IsUniqueConstraintViolation(exception))
        {
            // Another account claimed the email between the validation above and
            // the save. Translate the database race into the same conflict signal.
            throw new DuplicateEmailException();
        }

        return existingUser;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var user = await _dbContext.Users.FindAsync([id], cancellationToken);
        if (user is null)
        {
            return false;
        }

        var isAdmin = await _dbContext.UserRoles.AnyAsync(
            userRole => userRole.UserId == id && userRole.RoleName == AppRoles.Admin, cancellationToken);
        if (isAdmin && await _dbContext.UserRoles.CountAsync(
                userRole => userRole.RoleName == AppRoles.Admin, cancellationToken) <= 1)
        {
            return false;
        }

        _dbContext.Follows.RemoveRange(
            _dbContext.Follows.Where(follow => follow.FollowerId == id || follow.FollowingId == id));
        _dbContext.UserBlocks.RemoveRange(
            _dbContext.UserBlocks.Where(block => block.BlockerId == id || block.BlockedId == id));
        _dbContext.Messages.RemoveRange(
            _dbContext.Messages.Where(message => message.SenderId == id || message.RecipientId == id));
        _dbContext.SceneRatings.RemoveRange(
            _dbContext.SceneRatings.Where(rating => rating.UserId == id));
        var reportsAboutUser = await _dbContext.ContentReports
            .Where(report => report.TargetType == ContentReportTargets.Profile
                && report.TargetId == id
                && report.Status != ContentReportStatuses.Dismissed
                && report.Status != ContentReportStatuses.Actioned)
            .ToListAsync(cancellationToken);
        foreach (var report in reportsAboutUser)
        {
            report.Status = ContentReportStatuses.Actioned;
            report.ActionTaken = "AccountDeleted";
            report.ResolutionNotes ??= "Account deleted by an administrator or account owner.";
            report.ResolvedAt = DateTimeOffset.UtcNow;
        }
        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}