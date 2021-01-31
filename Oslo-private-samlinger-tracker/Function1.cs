using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

using SendGrid;
using SendGrid.Helpers.Mail;
using System.Threading.Tasks;

namespace Oslo_private_samlinger_tracker
{
    public static class Function1
    {
        [FunctionName("Function1")]
        public static async Task Run([TimerTrigger("0 */1 * * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            string siteUrl = "https://www.oslo.kommune.no/koronavirus/rad-og-regler-i-oslo/private-samlinger";
            var html = new System.Net.WebClient().DownloadString(siteUrl);
            log.LogInformation(html);


            var apiKey = Environment.GetEnvironmentVariable("SendGridApiKey");
            var client = new SendGridClient(apiKey);
            var from = new EmailAddress("test@example.com", "Example User");
            var subject = "Sending with SendGrid is Fun";
            var to = new EmailAddress("test@example.com", "Example User");
            var plainTextContent = "and easy to do anywhere, even with C#";
            var htmlContent = "<strong>and easy to do anywhere, even with C#</strong>";
            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);
            var response = await client.SendEmailAsync(msg);
        }
    }
}
