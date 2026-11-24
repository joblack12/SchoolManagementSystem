using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Data;
using SchoolManagementSystem.Data.Entities;
using SchoolManagementSystem.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SchoolManagementSystem.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly SchoolDbContext _context;
        private readonly UserManager<User> _userManager;

        public ReportsController(SchoolDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Reports/ReportCard?studentId=5
        [Authorize(Roles = "Admin,Employee,Teacher,Student")]
        public async Task<IActionResult> ReportCard(int? studentId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            // Enforce Student isolation: Students can only view their own report card
            if (User.IsInRole("Student"))
            {
                var studentRecord = await _context.Students
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.UserId == currentUser.Id);

                if (studentRecord == null)
                    return NotFound("Student profile not linked to your account.");

                studentId = studentRecord.Id;
            }
            else if (studentId == null)
            {
                return RedirectToAction(nameof(StudentList));
            }

            var student = await _context.Students
                .AsNoTracking()
                .Include(s => s.SchoolClass)
                    .ThenInclude(sc => sc.Course)
                .FirstOrDefaultAsync(s => s.Id == studentId);

            if (student == null) return NotFound();

            // Scope Teachers strictly to students in their assigned classes
            if (User.IsInRole("Teacher") && !User.IsInRole("Admin") && !User.IsInRole("Employee"))
            {
                var teacher = await _context.Teachers
                    .AsNoTracking()
                    .Include(t => t.TeacherSchoolClasses)
                    .FirstOrDefaultAsync(t => t.UserId == currentUser.Id);

                if (teacher == null || student.SchoolClassId == null ||
                    !teacher.TeacherSchoolClasses.Any(tsc => tsc.SchoolClassId == student.SchoolClassId.Value))
                {
                    return Forbid();
                }
            }

            var grades = await _context.Grades
                .AsNoTracking()
                .Include(g => g.Subject)
                .Where(g => g.StudentId == student.Id)
                .ToListAsync();

            var viewModel = new StudentReportCardViewModel
            {
                StudentId = student.Id,
                FullName = $"{student.FirstName} {student.LastName}",
                AdmissionNumber = $"ADM-{student.Id:D4}",
                ClassName = student.SchoolClass?.ClassName ?? "Unassigned",
                CourseName = student.SchoolClass?.Course?.Name ?? "General",
                Subjects = grades.Select(g => new SubjectGradeItem
                {
                    SubjectCode = $"SUB-{g.SubjectId:D3}",
                    SubjectName = g.Subject?.Name ?? "Subject",
                    Credits = g.Subject?.Credits ?? 0,
                    Score = g.Value,
                    Status = g.Status
                }).ToList()
            };

            return View(viewModel);
        }

        [Authorize(Roles = "Admin,Employee,Teacher")]
        public async Task<IActionResult> StudentList()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            IQueryable<Student> query = _context.Students
                .AsNoTracking()
                .Include(s => s.SchoolClass);

            if (User.IsInRole("Teacher") && !User.IsInRole("Admin") && !User.IsInRole("Employee"))
            {
                var teacher = await _context.Teachers
                    .AsNoTracking()
                    .Include(t => t.TeacherSchoolClasses)
                    .FirstOrDefaultAsync(t => t.UserId == currentUser.Id);

                var allowedClassIds = teacher?.TeacherSchoolClasses
                    .Select(tsc => tsc.SchoolClassId)
                    .ToList() ?? new List<int>();

                query = query.Where(s => s.SchoolClassId.HasValue && allowedClassIds.Contains(s.SchoolClassId.Value));
            }

            var students = await query
                .OrderBy(s => s.LastName)
                .ToListAsync();

            return View(students);
        }
    }
}
