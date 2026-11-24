using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagementSystem.Data.Entities;
using SchoolManagementSystem.Helpers;
using SchoolManagementSystem.Models;
using SchoolManagementSystem.Repositories;

namespace SchoolManagementSystem.Controllers
{
    [Authorize]
    public class AlertController : Controller
    {
        private readonly IAlertRepository _alertRepository;
        private readonly IUserHelper _userHelper;

        public AlertController(IAlertRepository alertRepository, IUserHelper userHelper)
        {
            _alertRepository = alertRepository;
            _userHelper = userHelper;
        }

        [HttpGet]
        public async Task<IActionResult> Notifications()
        {
            var alerts = await _alertRepository.GetActiveAlertsAsync();
            return View(alerts);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Employee")]
        public async Task<IActionResult> UnresolvedAlerts()
        {
            var alerts = await _alertRepository.GetActiveAlertsAsync();
            return View(alerts);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Employee,Teacher")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Employee,Teacher")]
        public async Task<IActionResult> Create(AlertViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userHelper.GetUserByEmailAsync(User.Identity?.Name);
                if (user == null)
                {
                    return Forbid();
                }

                var employee = await _userHelper.GetEmployeeByUserAsync(User.Identity?.Name);

                var alert = new Alert
                {
                    Message = model.Message,
                    CreatedAt = DateTime.UtcNow,
                    IsResolved = false,
                    UserId = user.Id,
                    EmployeeId = employee?.Id
                };

                await _alertRepository.CreateAlertAsync(alert);
                return RedirectToAction(nameof(ViewAlerts));
            }

            return View(model);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Employee,Teacher,Student")]
        public async Task<IActionResult> ViewAlerts()
        {
            var employee = await _userHelper.GetEmployeeByUserAsync(User.Identity?.Name);

            if (employee != null && (User.IsInRole("Admin") || User.IsInRole("Employee")))
            {
                var employeeAlerts = await _alertRepository.GetAlertsByEmployeeIdAsync(employee.Id);
                return View(employeeAlerts);
            }

            var activeAlerts = await _alertRepository.GetActiveAlertsAsync();
            return View(activeAlerts);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Employee")]
        public async Task<IActionResult> MarkAsResolved(int id)
        {
            await _alertRepository.MarkAsResolvedAsync(id);
            return RedirectToAction(nameof(UnresolvedAlerts));
        }
    }
}
