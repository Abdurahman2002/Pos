using Microsoft.EntityFrameworkCore;
using NewsApp2.Models.Interfaces;
using System.Linq.Expressions;
using System.Collections.Concurrent;

namespace NewsApp2.Models.Repositories
{
    public class GRepository<T> : IGRepository<T> where T : class
    {
        private static readonly ConcurrentDictionary<Type, string[]> KeyNamesCache = new();

        private readonly AppDbContext _context;
        private DbSet<T> _dbSet = null;
        public GRepository(AppDbContext context)
        {
            _context = context;
            _dbSet = _context.Set<T>();
        }
        public void Insert(T entity)
        {
            _dbSet.Add(entity);
        }
        public void Delete(T entity)
        {

            _dbSet.Remove(entity);
        }
        public void Update(T entity)
        {
            var keyNames = KeyNamesCache.GetOrAdd(typeof(T), static type => Array.Empty<string>());
            if (keyNames.Length == 0)
            {
                var entityType = _context.Model.FindEntityType(typeof(T));
                var primaryKey = entityType?.FindPrimaryKey();
                keyNames = primaryKey?.Properties.Select(p => p.Name).ToArray() ?? Array.Empty<string>();
                KeyNamesCache[typeof(T)] = keyNames;
            }

            if (keyNames.Length == 0)
            {
                _dbSet.Update(entity);
                return;
            }

            var keyValues = keyNames
                .Select(name => _context.Entry(entity).Property(name).CurrentValue)
                .ToArray();

            var trackedEntity = _dbSet.Local.FirstOrDefault(localEntity =>
            {
                var localEntry = _context.Entry(localEntity);
                for (var i = 0; i < keyNames.Length; i++)
                {
                    var keyName = keyNames[i];
                    var localValue = localEntry.Property(keyName).CurrentValue;
                    if (!Equals(localValue, keyValues[i]))
                        return false;
                }

                return true;
            });

            if (trackedEntity != null)
            {
                _context.Entry(trackedEntity).CurrentValues.SetValues(entity);
                _context.Entry(trackedEntity).State = EntityState.Modified;
                return;
            }

            _dbSet.Attach(entity);
            _context.Entry(entity).State = EntityState.Modified;
        }

        public IQueryable<T> GetAll(bool tracking = false)
        {
            return tracking ? _dbSet : _dbSet.AsNoTracking();
        }
        public IQueryable<T> GetWhere(Expression<Func<T, bool>> filter, bool tracking = false)
        {
            return tracking ? _dbSet.Where(filter) : _dbSet.Where(filter).AsNoTracking();
        }
        public async Task<T> GetByIdAsync(object id)
        {
            return await _dbSet.FindAsync(id);
        }

        public IQueryable<T> Include(params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = _dbSet;
            foreach (Expression<Func<T, object>> include in includes)
                query = query.Include(include);

            return query.AsNoTracking();
        }

    }

}
