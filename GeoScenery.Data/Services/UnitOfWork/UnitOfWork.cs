using System.Linq;
using GeoScenery.Data.Context;
using GeoScenery.Data.Models;
using GeoScenery.Data.Repository;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace GeoScenery.Data.Services
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly MyProjectDbContext _dbContext;

        #region Repositories
        public IRepository<User> UserRepository =>
            new Repository<User>(_dbContext);

        #endregion

        public UnitOfWork(MyProjectDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task Commit()
        {
            await _dbContext.SaveChangesAsync();
        }
        public void Dispose()
        {
            _dbContext.Dispose();
        }
        public void RejectChanges()
        {
            foreach (var entry in _dbContext.ChangeTracker.Entries()
                .Where(e => e.State != EntityState.Unchanged))
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.State = EntityState.Detached;
                        break;
                    case EntityState.Modified:
                    case EntityState.Deleted:
                        entry.Reload();
                        break;
                }
            }
        }
    }
}
