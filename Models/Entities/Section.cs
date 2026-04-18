using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewsApp2.Models.Entities
{
    public class Section : BaseEntity
    {
        [MaxLength(50)]
        [MinLength(3)]
        [Remote(action: "NameExists", controller: "Sections")]
        public required  string Name { get; set; }

        public List<News>? News { get; set; }

        [NotMapped]
        public int SectionNewsCount { get; set; }
    }



}
