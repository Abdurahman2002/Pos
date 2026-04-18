using System.ComponentModel.DataAnnotations;

namespace NewsApp2.ViewModels.Identity
{
    public class EditUserVM
    {
        public EditUserVM()
        {
            Roles = new List<string>(); //وتسبب أخطاء أثناء الاستخدام null تنشئ قائمة فارغة حتى لا تكون
            Claims = new List<string>();
        }
        public string Id { get; set; }

        // --------Basic User Info---------
        [Required]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        //------------Lockout----------------
        [Display(Name = "Lockout End")]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm:ss tt}")]
        public DateTimeOffset? LockoutEnd { get; set; }

        public bool EmailConfirmed { get; set; }

        //-------------System Tracking----------------
        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; }

        [Display(Name = "Modified Date")]
        public DateTime? ModifiedDate { get; set; }

        [Display(Name = "Last Access Time")]
        public DateTime? LastAccessTime { get; set; }

        //----------------Roles & Claims----------------
        public List<string> Roles { get; set; }
        public List<string> Claims { get; set; }



        //----------------Local-Time Display Properties----------------
        public DateTime CreatedDateLocalTime
        {
            get => CreatedDate.ToLocalTime();
        }

        public DateTime? ModifiedDateLocalTime
        {
            get => ModifiedDate?.ToLocalTime();
        }

        public DateTime? LastAccessTimeLocalTime
        {
            get => LastAccessTime?.ToLocalTime();
        }

        public DateTimeOffset? LockoutEndTimeLocalTime
        {
            get => LockoutEnd?.ToLocalTime();
        }


    }
}
