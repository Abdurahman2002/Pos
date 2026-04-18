using NewsApp2.Models.Interfaces;
using NewsApp2.Models.Repositories;

namespace NewsApp2.Models.UnitOfWork
{
    public class UnitOfWork<T> : IUnitOfWork<T>, IDisposable where T : class
    {
        private readonly AppDbContext _context;
        private IGRepository<T> _entity;
        private bool disposed = false;

        public UnitOfWork(AppDbContext context)
        {
            _context = context;
        }

        public IGRepository<T> Repository
        {
            get
            {
                return _entity ?? (_entity = new GRepository<T>(_context));
            }
        }


        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }


        //Disposable Pattern
        //Connections + ChangeTracker لإغلاق اتصال قاعدة البيانات وتحرير موارد النظام والذاكرة المستخدمة مثل
        //تلقائيا Dispose باستدعاء DI Container يقوم Request عند نهاية
        //Dependency Injection أي أنه يتم استدعاؤه تلقائيا عند استخدام
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    _context.Dispose();
                }
                disposed = true;
            }
        }

        public void Dispose() // نحتاجه عند الاستدعاء يدويا ولكنه مفيد لتحسين الاداء في حالات معينة
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


    }


}
