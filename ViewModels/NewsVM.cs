using NewsApp2.Models.Entities;

namespace NewsApp2.ViewModels
{
    public class NewsVM
    {
        public News News { get; set; }
        public List<News> NewsList { get; set; }
    }
}
