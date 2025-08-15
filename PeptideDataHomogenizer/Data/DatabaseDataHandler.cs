using Entities;
using Microsoft.EntityFrameworkCore;

namespace PeptideDataHomogenizer.Data
{
    public class DatabaseDataHandler
    {
        private readonly DbContext _context;

        public DatabaseDataHandler(DbContext context)
        {
            _context = context;
            _context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }

        public async Task AddAsync<T>(T entity) where T : class
        {

            _context.ChangeTracker.Clear();
            await _context.Set<T>().AddAsync(entity);
            await _context.SaveChangesAsync();
        }

        public async Task AddRangeAsync<T>(IEnumerable<T> entities) where T : class
        {
            _context.ChangeTracker.Clear();
            await _context.Set<T>().AddRangeAsync(entities);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync<T>(T entity) where T : class
        {

            _context.ChangeTracker.Clear();
            _context.Set<T>().Update(entity);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync<T>(T entity) where T : class
        {
            _context.ChangeTracker.Clear();
            _context.Set<T>().Remove(entity);
            await _context.SaveChangesAsync();
        }

        public async Task<List<T>> GetAsync<T>(Func<IQueryable<T>, IQueryable<T>> query = null) where T : class
        {
            _context.ChangeTracker.Clear();
            var dbSet = _context.Set<T>().AsNoTracking();
            if (query != null)
            {
                dbSet = query(dbSet);
            }
            return await dbSet.ToListAsync();
        }


        public async Task<T> GetByIdAsync<T>(params object[] keyValues) where T : class
        {
            return await _context.Set<T>().FindAsync(keyValues);
        }

        public async Task<List<T>> GetAllAsync<T>() where T : class
        {
            _context.ChangeTracker.Clear();
            return await _context.Set<T>().AsNoTracking().ToListAsync();
        }

        public async Task<int> GetCountAsync<T>() where T : class
        {
            return await _context.Set<T>().CountAsync();
        }

        public async Task<int> ExecuteSqlCommandAsync(string sql, params object[] parameters)
        {
            return await _context.Database.ExecuteSqlRawAsync(sql, parameters);
        }
    }
}
