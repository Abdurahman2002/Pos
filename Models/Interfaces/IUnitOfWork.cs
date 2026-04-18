namespace NewsApp2.Models.Interfaces
{
    public interface IUnitOfWork<T>
    {
        IGRepository<T> Repository { get; }
        Task SaveAsync();
    }

}
