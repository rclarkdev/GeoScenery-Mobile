using GeoScenery.Data.Context;
using GeoScenery.Data.Models;
using Microsoft.EntityFrameworkCore;

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

        existingUser.DisplayName = user.DisplayName;
        existingUser.Email = user.Email;
        existingUser.ProfileImageUrl = user.ProfileImageUrl;
        existingUser.Latitude = user.Latitude;
        existingUser.Longitude = user.Longitude;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return existingUser;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FindAsync([id], cancellationToken);
        if (user is null)
        {
            return false;
        }

        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}