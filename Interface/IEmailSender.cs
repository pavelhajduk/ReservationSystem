using System.Threading.Tasks;

namespace IdentityMongo.Interface
{
    public interface IEmailSender
    {
        Task<string> SendEmailAsync(string recipientEmail, string recipientFirstName, string Link);
    }
}
