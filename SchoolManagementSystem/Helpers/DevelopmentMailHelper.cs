using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace SchoolManagementSystem.Helpers
{
    public class DevelopmentMailHelper : IMailHelper
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<DevelopmentMailHelper> _logger;

        public DevelopmentMailHelper(IWebHostEnvironment env, ILogger<DevelopmentMailHelper> logger)
        {
            _env = env;
            _logger = logger;
        }

        public Response SendEmail(string to, string subject, string body)
        {
            try
            {
                var folder = Path.Combine(_env.ContentRootPath, "DevEmails");
                Directory.CreateDirectory(folder);
                var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid()}.html";
                var path = Path.Combine(folder, fileName);
                File.WriteAllText(path, $"To: {to}\nSubject: {subject}\n\n{body}");
                _logger.LogInformation("Development email saved to {Path}", path);
                return new Response { IsSuccess = true, Message = $"Saved to {path}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save development email");
                return new Response { IsSuccess = false, Message = ex.ToString() };
            }
        }
    }
}