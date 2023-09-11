using System.Threading.Tasks;

namespace PSRes.Interface
{
    public interface IEmailSender
    {
        Task<string> SendEmailAsync(string recipientEmail, string recipientFirstName, string Link);
    }
}
