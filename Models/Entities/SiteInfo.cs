
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewsApp2.Models.Entities
{
    public class SiteInfo : BaseEntity
    {
        [StringLength(50, ErrorMessage = "{0} length must be between {2} and {1}.", MinimumLength = 3)]
        [RegularExpression(@"^[a-zA-Z][a-zA-Z0-9]+$", ErrorMessage = "The string must start with a letter followed by letters and numbers only.")]
        public required string Name { get; set; }

        [StringLength(200)]
        public required string Activity { get; set; }

        [StringLength(2000)]
        public required string About { get; set; }

        [Display(Name = "Site Logo")]
        public string? LogoUrl { get; set; }

        [Display(Name = "Cover Image")]
        public string? CoverImageUrl { get; set; }

        [NotMapped]
        public IFormFile? Logo { get; set; }

        [NotMapped]
        public IFormFile? CoverImage { get; set; }

    }




}
