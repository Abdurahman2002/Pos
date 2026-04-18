using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Mvc;
using MimeKit;
using NewsApp2.Classes;
using NewsApp2.Models.Entities;
using NewsApp2.Models.Interfaces;

namespace NewsApp2.Models.Repositories
{
    public class EmailSender : IEmailSender
    {
        private readonly MailSettings _mailSettings;
        private readonly IUnitOfWork<SiteInfo> _siteInfo;
        private readonly IUnitOfWork<Contact> _contact;
        private readonly IHttpContextAccessor _httpContext;

        public EmailSender(MailSettings mailSettings,
                           IUnitOfWork<SiteInfo> siteInfo,
                           IUnitOfWork<Contact> contact,
                           IHttpContextAccessor httpContext)
        {
            _mailSettings = mailSettings;
            _siteInfo = siteInfo;
            _contact = contact;
            _httpContext = httpContext;
        }

        public async Task SendEmailAsync(Message message)
        {
            var mimeMessage = CreateMimeMessage(message);
            await SendAsync(mimeMessage);
        }

        private MimeMessage CreateMimeMessage(Message message)
        {
            var mimeMessage = new MimeMessage();

            mimeMessage.From.Add(new MailboxAddress(_mailSettings.DisplayName, _mailSettings.UserName));

            mimeMessage.To.AddRange(message.To);
            mimeMessage.Subject = message.Subject;


            //----------------------SiteInfo & Contacts------------------------------
            var siteInfo = _siteInfo.Repository.GetAll().FirstOrDefault();
            var contact = _contact.Repository.GetAll().FirstOrDefault();
           

            message.Content = message.Content.Replace("{SiteName}", siteInfo?.Name ?? "");
            message.Content = message.Content.Replace("{Mail}", contact?.Email ?? "");
            message.Content = message.Content.Replace("{Phone}", contact?.Phone ?? "");
            
            //--------------------domain----------------------------------------------------
            var request = _httpContext.HttpContext?.Request;
            var domain = $"{request?.Scheme}://{request?.Host.Value}";
            message.Content = message.Content.Replace("{domain}", domain);
            //-----------------------------------------------------------------------------



            var bodyBuilder = new BodyBuilder { HtmlBody = message.Content };
            if (message.Attachments != null && message.Attachments.Count > 0)
            {
                foreach (var attachment in message.Attachments)
                {
                    using var stream = attachment.OpenReadStream();
                    bodyBuilder.Attachments.Add(attachment.FileName, stream, ContentType.Parse(attachment.ContentType));
                }

            }
            mimeMessage.Body = bodyBuilder.ToMessageBody();
            //---------------------------------------------------------------------
            return mimeMessage;
        }

        private async Task SendAsync(MimeMessage mimeMessage)
        {
            using var client = new SmtpClient();

            try
            {
                await client.ConnectAsync(_mailSettings.Host, _mailSettings.Port,false);
                await client.AuthenticateAsync(_mailSettings.UserName, _mailSettings.Password);
                await client.SendAsync(mimeMessage);
            }
            catch
            {
                throw;
            }
            finally
            {
                await client.DisconnectAsync(true);
            }

        }
    }
}
