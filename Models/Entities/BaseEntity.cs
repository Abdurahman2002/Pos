using System.ComponentModel.DataAnnotations;

namespace NewsApp2.Models.Entities
{
    public class BaseEntity
    {
        public Guid Id { get; set; }

        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm:ss tt}")]
        public DateTime Created { get; set; } = DateTime.UtcNow;

        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm:ss tt}")]
        public DateTime? Modified { get; set; }


        //-------------------------For User Profile-------------------------
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public DateTime CreatedDateLocalTime => Created.ToLocalTime();

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public DateTime? ModifiedDateLocalTime => Modified?.ToLocalTime();
        //-------------------------------------------------------------------

    }

}
