using MimeKit;


namespace NewsApp2.Classes
{
    public class Message
    {
        public Message(IEnumerable<string> to, string subject, string content, IFormFileCollection attachments)
        {
            To = to.Select(x => new MailboxAddress("", x)).ToList();
            Subject = subject;
            Content = content;
            Attachments = attachments;
        }

        public List<MailboxAddress> To { get; set; }
        public string Subject { get; set; }
        public string Content { get; set; }
        public IFormFileCollection Attachments { get; set; }  // files

    }

}
