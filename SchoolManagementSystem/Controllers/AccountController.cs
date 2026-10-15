using Azure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SchoolManagementSystem.Data.Entities;
using SchoolManagementSystem.Helpers;
using SchoolManagementSystem.Models;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.CodeAnalysis.CSharp.SyntaxTokenParser;

namespace SchoolManagementSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserHelper _userHelper;
        private readonly IMailHelper _mailHelper;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            IUserHelper userHelper,
            IMailHelper mailHelper,
            IConfiguration configuration,
            ILogger<AccountController> logger)
        {
            _userHelper = userHelper;
            _mailHelper = mailHelper;
            _configuration = configuration;
            _logger = logger;
        }

        // Displays the login page
        public IActionResult Login()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        // Processes the login
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // Check that the user exists first to give clearer feedback
            var user = await _userHelper.GetUserByEmailAsync(model.Username);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return View(model);
            }

            var result = await _userHelper.LoginAsync(model);

            _logger.LogInformation("Login attempt for {Email}. Result: Succeeded={Succeeded}, LockedOut={LockedOut}, NotAllowed={NotAllowed}, TwoFactor={TwoFactor}",
                model.Username, result.Succeeded, result.IsLockedOut, result.IsNotAllowed, result.RequiresTwoFactor);

            if (result.Succeeded)
            {
                var userRole = await _userHelper.GetRoleAsync(user);

                return userRole switch
                {
                    "Admin" => RedirectToAction("AdminDashboard", "Dashboard"),
                    "Employee" => RedirectToAction("EmployeeDashboard", "Dashboard"),
                    "Teacher" => RedirectToAction("TeacherDashboard", "Dashboard"),
                    "Student" => RedirectToAction("StudentDashboard", "Dashboard"),
                    _ => RedirectToAction("Index", "Home"),
                };
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "Account locked. Please try later.");
            }
            else if (result.IsNotAllowed)
            {
                if (!user.EmailConfirmed)
                    ModelState.AddModelError(string.Empty, "Account not confirmed. Check your email for confirmation instructions.");
                else
                    ModelState.AddModelError(string.Empty, "Login not allowed. Contact support.");
            }
            else if (result.RequiresTwoFactor)
            {
                ModelState.AddModelError(string.Empty, "Two-factor authentication required. Complete 2FA to continue.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
            }

            return View(model);
        }
    }
}