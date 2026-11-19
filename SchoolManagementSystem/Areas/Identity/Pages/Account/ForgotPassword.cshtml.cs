#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using SchoolManagementSystem.Data.Entities;
using SchoolManagementSystem.Helpers;

namespace SchoolManagementSystem.Areas.Identity.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly IMailHelper _mailHelper;
        private readonly ILogger<ForgotPasswordModel> _logger;

        public ForgotPasswordModel(
            UserManager<User> userManager,
            IMailHelper mailHelper,
            ILogger<ForgotPasswordModel> logger)
        {
            _userManager = userManager;
            _mailHelper = mailHelper;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(Input.Email);
                if (user == null)
                {
                    // Don't reveal that the user does not exist
                    return RedirectToPage("./ForgotPasswordConfirmation");
                }

                var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page(
                    "/Account/ResetPassword",
                    pageHandler: null,
                    values: new { area = "Identity", code },
                    protocol: Request.Scheme);

                var emailBody = $@"
                    <h2>Reset Your Password</h2>
                    <p>Hello {user.FirstName},</p>
                    <p>We received a request to reset the password for your Ridgesview Schools account.</p>
                    <p><a href='{HtmlEncoder.Default.Encode(callbackUrl)}' style='display:inline-block;padding:10px 20px;background-color:#0d6efd;color:#ffffff;text-decoration:none;border-radius:5px;'>Reset Password</a></p>
                    <p>If you did not request a password reset, you can safely ignore this email.</p>";

                var response = _mailHelper.SendEmail(
                    Input.Email,
                    "Ridgesview Schools - Reset Password",
                    emailBody);

                if (!response.IsSuccess)
                {
                    _logger.LogError("Failed to send password reset email to {Email}: {Error}", Input.Email, response.Message);
                    ModelState.AddModelError(string.Empty, "Unable to send reset email right now. Please check SMTP configuration.");
                    return Page();
                }

                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            return Page();
        }
    }
}
