using NewsApp2.Classes;
using System.ComponentModel.DataAnnotations;

namespace NewsApp2.ViewModels
{
    public class EmailVM
    {
        public MailSettings MailSettings { get; set; }

        
        
        [DataType(DataType.EmailAddress, ErrorMessage = "Invalid email")]
        public string To { get; set; }
        public string Subject { get; set; }
        public string Content { get; set; }
        public IFormFileCollection? Attachments { get; set; } // عدة ملفات

    }
}
