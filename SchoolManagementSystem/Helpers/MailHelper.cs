using MailKit.Net.Smtp;
using MimeKit;

namespace SchoolManagementSystem.Helpers
{
    public class MailHelper : IMailHelper
    {
        private readonly IConfiguration _configuration;

        public MailHelper(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Response SendEmail(string to, string subject, string body)
        {
            var nameFrom = _configuration["Mail:NameFrom"];
            var from = _configuration["Mail:From"];
            var smtp = _configuration["Mail:Smtp"];
            var port = _configuration["Mail:Port"];
            var password = _configuration["Mail:Password"];

            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(from)) missing.Add("Mail:From");
            if (string.IsNullOrWhiteSpace(smtp)) missing.Add("Mail:Smtp");
            if (string.IsNullOrWhiteSpace(port)) missing.Add("Mail:Port");
            if (string.IsNullOrWhiteSpace(password)) missing.Add("Mail:Password");
            if (string.IsNullOrWhiteSpace(to)) missing.Add("to (method argument)");

            if (missing.Any())
            {
                return new Response
                {
                    IsSuccess = false,
                    Message = $"Missing mail configuration or parameters: {string.Join(", ", missing)}"
                };
            }

            if (!int.TryParse(port, out var portNumber))
            {
                return new Response
                {
                    IsSuccess = false,
                    Message = "Invalid Mail:Port value. It must be a number."
                };
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(nameFrom ?? string.Empty, from));
            message.To.Add(new MailboxAddress(to, to));
            message.Subject = subject;

            var bodybuilder = new BodyBuilder
            {
                HtmlBody = body,
            };
            message.Body = bodybuilder.ToMessageBody();

            try
            {
                using (var client = new SmtpClient())
                {
                    client.Connect(smtp, portNumber, false);
                    client.Authenticate(from, password);
                    client.Send(message);
                    client.Disconnect(true);
                }
            }
            catch (Exception ex)
            {
                return new Response
                {
                    IsSuccess = false,
                    Message = ex.ToString()
                };
            }

            return new Response
            {
                IsSuccess = true
            };
        }
    }
}