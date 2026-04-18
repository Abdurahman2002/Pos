//using Microsoft.DotNet.Scaffolding.Shared.Messaging;  // الانتباه من اضافة هذه المكتبة بدلا من النيم سبيس 

using NewsApp2.Classes;

namespace NewsApp2.Models.Interfaces
{
    public interface IEmailSender
    {
        Task SendEmailAsync(Message message);
    }
}
