using System;

namespace NewsApp2.ViewModels.Common
{
    /// <summary>
    /// Lightweight server-side pagination metadata passed to views (usually via ViewBag.Pagination).
    /// The page's rows are still passed as the view Model; this only carries paging state.
    /// </summary>
    public class PaginationVM
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public int TotalCount { get; set; }

        public int TotalPages => PageSize <= 0 ? 1 : (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPrevious => Page > 1;
        public bool HasNext => Page < TotalPages;
        public int FirstItem => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;
        public int LastItem => Math.Min(Page * PageSize, TotalCount);

        /// <summary>Clamps a requested page into the valid [1, TotalPages] range.</summary>
        public static int NormalizePage(int page) => page < 1 ? 1 : page;
    }
}
