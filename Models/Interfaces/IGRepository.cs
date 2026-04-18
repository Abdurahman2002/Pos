using System.Linq.Expressions;

namespace NewsApp2.Models.Interfaces
{
    public interface IGRepository<T>
    {
        void Insert(T entity);
        void Delete(T entity);
        void Update(T entity);

        IQueryable<T> GetAll(bool tracking = false);
        IQueryable<T> GetWhere(Expression<Func<T, bool>> filter, bool tracking = false);
        Task<T> GetByIdAsync(object id);
        IQueryable<T> Include(params Expression<Func<T, object>>[] includes);
    }

}
