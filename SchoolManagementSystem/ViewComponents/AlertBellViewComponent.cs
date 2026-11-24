using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Data;

namespace SchoolManagementSystem.ViewComponents
{
    public class AlertBellViewComponent : ViewComponent
    {
        private readonly SchoolDbContext _context;

        public AlertBellViewComponent(SchoolDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var activeAlerts = await _context.Alerts
                .Include(a => a.Employee)
                .Include(a => a.User)
                .Where(a => !a.IsResolved)
                .OrderByDescending(a => a.CreatedAt)
                .Take(5)
                .ToListAsync();

            ViewBag.UnreadCount = await _context.Alerts.CountAsync(a => !a.IsResolved);
            return View(activeAlerts);
        }

    }
}
