using System.Net.Mail;
using System.Net;
using Microsoft.Extensions.Logging;
using EnergyIOT.Models;

namespace EnergyIOT
{
    internal static class SendEmail
    {
        internal static void SendEmailMsg(EmailConfig emailConfig, ILogger logger, string subject, string message)
        {
            if (message.Trim() != "")
            {
                var smtpClient = new SmtpClient(emailConfig.Server)
                {
                    Port = emailConfig.Port,
                    EnableSsl = emailConfig.SSL,
                    Credentials = new NetworkCredential(emailConfig.Username, emailConfig.Pwd),
                    Host = emailConfig.Server
                };

                MailMessage msg = new MailMessage(new MailAddress(emailConfig.From, "EnergyIOT"),
                                                new MailAddress(emailConfig.To, emailConfig.To));
                msg.IsBodyHtml = true;
                msg.Body = message;
                msg.Subject = subject;

                try
                {
                    smtpClient.Send(msg);
                }
                catch (Exception ex)
                {
                    logger.LogError("SMTPClient Fail:{error}", ex.ToString());
                    Console.WriteLine(ex.ToString());
                }

                smtpClient.Dispose();
            }
        }
    }
}