using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Data;
using SchoolManagementSystem.Data.Entities;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SchoolManagementSystem.Controllers
{
    [Authorize]
    public class BillingController : Controller
    {
        private readonly SchoolDbContext _context;
        private readonly UserManager<User> _userManager;

        public BillingController(SchoolDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Billing/Statement?studentId=5
        [Authorize(Roles = "Admin,Employee,Student")]
        public async Task<IActionResult> Statement(int? studentId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            if (User.IsInRole("Student"))
            {
                var student = await _context.Students
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.UserId == currentUser.Id);
                if (student == null) return NotFound("Student profile not linked.");
                studentId = student.Id;
            }
            else if (studentId == null)
            {
                return RedirectToAction(nameof(Index));
            }

            var targetStudent = await _context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == studentId.Value);
            if (targetStudent == null) return NotFound();

            var invoices = await _context.FeeInvoices
                .AsNoTracking()
                .Include(i => i.Student)
                .Include(i => i.Payments)
                .Where(i => i.StudentId == studentId.Value)
                .OrderByDescending(i => i.DueDate)
                .ToListAsync();

            ViewBag.Student = targetStudent;
            return View(invoices);
        }

        // Admin & Employee: Overview of all invoices & balances
        [Authorize(Roles = "Admin,Employee")]
        public async Task<IActionResult> Index()
        {
            var invoices = await _context.FeeInvoices
                .AsNoTracking()
                .Include(i => i.Student)
                .Include(i => i.Payments)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            return View(invoices);
        }

        // Admin & Employee: Create new Fee Invoice
        [Authorize(Roles = "Admin,Employee")]
        public async Task<IActionResult> CreateInvoice()
        {
            await PopulateStudentsDropDownAsync();
            return View(new FeeInvoice { DueDate = DateTime.Today.AddDays(30) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Employee")]
        public async Task<IActionResult> CreateInvoice(FeeInvoice invoice)
        {
            ModelState.Remove(nameof(FeeInvoice.Student));

            if (invoice.TotalAmount <= 0)
            {
                ModelState.AddModelError(nameof(FeeInvoice.TotalAmount), "Invoice amount must be greater than zero.");
            }

            if (!await _context.Students.AnyAsync(s => s.Id == invoice.StudentId))
            {
                ModelState.AddModelError(nameof(FeeInvoice.StudentId), "Please select a valid student.");
            }

            if (ModelState.IsValid)
            {
                invoice.Id = 0;
                invoice.CreatedAt = DateTime.UtcNow;

                _context.FeeInvoices.Add(invoice);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Fee invoice created successfully.";
                return RedirectToAction(nameof(Index));
            }

            await PopulateStudentsDropDownAsync();
            return View(invoice);
        }

        // Admin & Employee: Record a Payment against an Invoice
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Employee")]
        public async Task<IActionResult> RecordPayment(int feeInvoiceId, decimal amountPaid, string paymentMethod, string? transactionReference)
        {
            var invoice = await _context.FeeInvoices
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.Id == feeInvoiceId);

            if (invoice == null) return NotFound();

            if (amountPaid > invoice.Balance)
            {
                TempData["ErrorMessage"] = $"Payment amount ({amountPaid:N2}) cannot exceed the remaining balance ({invoice.Balance:N2}).";
                return RedirectToAction(nameof(Statement), new { studentId = invoice.StudentId });
            }


            if (string.IsNullOrWhiteSpace(paymentMethod))
            {
                TempData["ErrorMessage"] = "Please select a valid payment method.";
                return RedirectToAction(nameof(Statement), new { studentId = invoice.StudentId });
            }

            var totalAlreadyPaid = invoice.Payments?.Sum(p => p.AmountPaid) ?? 0m;
            var remainingBalance = invoice.TotalAmount - totalAlreadyPaid;

            if (amountPaid > remainingBalance)
            {
                TempData["ErrorMessage"] = $"Payment amount ({amountPaid:N2}) cannot exceed the remaining balance ({remainingBalance:N2}).";
                return RedirectToAction(nameof(Statement), new { studentId = invoice.StudentId });
            }

            var payment = new FeePayment
            {
                FeeInvoiceId = feeInvoiceId,
                AmountPaid = amountPaid,
                PaymentMethod = paymentMethod.Trim(),
                TransactionReference = transactionReference?.Trim(),
                PaymentDate = DateTime.UtcNow
            };

            _context.FeePayments.Add(payment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Payment recorded successfully.";
            return RedirectToAction(nameof(Statement), new { studentId = invoice.StudentId });
        }

        // Printable Official Payment Receipt
        [Authorize(Roles = "Admin,Employee,Student")]
        public async Task<IActionResult> Receipt(int paymentId)
        {
            var payment = await _context.FeePayments
                .AsNoTracking()
                .Include(p => p.FeeInvoice)
                    .ThenInclude(i => i.Student)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null) return NotFound();

            // Prevent a Student from viewing another student's receipt
            if (User.IsInRole("Student"))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null || payment.FeeInvoice?.Student?.UserId != currentUser.Id)
                    return Forbid();
            }

            return View(payment);
        }

        private async Task PopulateStudentsDropDownAsync()
        {
            var students = await _context.Students
                .AsNoTracking()
                .OrderBy(s => s.LastName)
                .Select(s => new { s.Id, FullName = $"{s.FirstName} {s.LastName} (ADM-{s.Id:D4})" })
                .ToListAsync();

            ViewBag.Students = new SelectList(students, "Id", "FullName");
        }
    }
}
