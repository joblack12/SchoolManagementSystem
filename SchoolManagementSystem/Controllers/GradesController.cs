using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using SchoolManagementSystem.Data.Entities;
using SchoolManagementSystem.Helpers;
using SchoolManagementSystem.Models;
using SchoolManagementSystem.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SchoolManagementSystem.Controllers
{
    [Authorize]
    public class GradesController : Controller
    {
        private readonly IGradeRepository _gradeRepository;
        private readonly IStudentRepository _studentRepository;
        private readonly ISchoolClassRepository _schoolClassRepository;
        private readonly ITeacherRepository _teacherRepository;
        private readonly IConverterHelper _converterHelper;
        private readonly ISubjectRepository _subjectRepository;
        private readonly ILogger<GradesController> _logger;

        public GradesController(
            IGradeRepository gradeRepository,
            IStudentRepository studentRepository,
            ISchoolClassRepository schoolClassRepository,
            ITeacherRepository teacherRepository,
            IConverterHelper converterHelper,
            ISubjectRepository subjectRepository,
            ILogger<GradesController> logger)
        {
            _gradeRepository = gradeRepository;
            _studentRepository = studentRepository;
            _schoolClassRepository = schoolClassRepository;
            _teacherRepository = teacherRepository;
            _converterHelper = converterHelper;
            _subjectRepository = subjectRepository;
            _logger = logger;
        }

        private async Task<Teacher?> GetCurrentTeacherWithDetailsAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return null;

            var teacher = await _teacherRepository.GetTeacherByUserIdAsync(userId);
            if (teacher == null) return null;

            return await _teacherRepository.GetTeacherWithDetailsAsync(teacher.Id);
        }

        private async Task<bool> CanAccessStudentAndSubjectAsync(Student student, int? subjectId = null)
        {
            if (User.IsInRole("Admin") || User.IsInRole("Employee")) return true;

            if (User.IsInRole("Teacher"))
            {
                var teacher = await GetCurrentTeacherWithDetailsAsync();
                if (teacher == null || student.SchoolClassId == null) return false;

                bool teachesClass = teacher.TeacherSchoolClasses.Any(tsc => tsc.SchoolClassId == student.SchoolClassId.Value);
                if (!teachesClass) return false;

                if (subjectId.HasValue)
                {
                    bool teachesSubject = teacher.TeacherSubjects.Any(ts => ts.SubjectId == subjectId.Value);
                    if (!teachesSubject) return false;
                }

                return true;
            }

            return false;
        }

        // GET: Grades
        [Authorize(Roles = "Employee,Admin,Teacher")]
        public async Task<IActionResult> Index(int? classId)
        {
            try
            {
                var schoolClasses = await _schoolClassRepository.GetAllAsync();

                if (User.IsInRole("Teacher") && !User.IsInRole("Admin") && !User.IsInRole("Employee"))
                {
                    var teacher = await GetCurrentTeacherWithDetailsAsync();
                    var allowedClassIds = teacher?.TeacherSchoolClasses.Select(tsc => tsc.SchoolClassId).ToHashSet() ?? new HashSet<int>();
                    schoolClasses = schoolClasses.Where(sc => allowedClassIds.Contains(sc.Id)).ToList();

                    if (classId.HasValue && !allowedClassIds.Contains(classId.Value))
                    {
                        return Forbid();
                    }
                }

                ViewBag.Classes = schoolClasses.Select(sc => new SelectListItem
                {
                    Value = sc.Id.ToString(),
                    Text = sc.ClassName
                }).ToList();

                if (classId.HasValue)
                {
                    var students = await _studentRepository.GetStudentsBySchoolClassIdAsync(classId.Value);
                    var studentAverages = students.Select(student => new StudentGradeAverageViewModel
                    {
                        Student = student
                    }).ToList();

                    return View(studentAverages);
                }

                return View(new List<StudentGradeAverageViewModel>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading grades list.");
                ModelState.AddModelError(string.Empty, "An error occurred while loading the grades list. Please try again later.");
                return View(new List<StudentGradeAverageViewModel>());
            }
        }

        // GET: Grades/Details/5
        [Authorize(Roles = "Employee,Admin,Teacher")]
        public async Task<IActionResult> Details(int studentId)
        {
            try
            {
                var student = await _studentRepository.GetByIdAsync(studentId);
                if (student == null) return new NotFoundViewResult("GradeNotFound");

                if (!await CanAccessStudentAndSubjectAsync(student)) return Forbid();

                var subjects = await _gradeRepository.GetSubjectsByStudentIdAsync(studentId);
                var grades = await _gradeRepository.GetGradesByStudentIdAsync(studentId);

                if (User.IsInRole("Teacher") && !User.IsInRole("Admin") && !User.IsInRole("Employee"))
                {
                    var teacher = await GetCurrentTeacherWithDetailsAsync();
                    var allowedSubjectIds = teacher?.TeacherSubjects.Select(ts => ts.SubjectId).ToHashSet() ?? new HashSet<int>();
                    subjects = subjects.Where(s => allowedSubjectIds.Contains(s.Id)).ToList();
                }

                var model = subjects.Select(subject => new StudentSubjectGradeViewModel
                {
                    Subject = subject,
                    Grade = grades.FirstOrDefault(g => g.SubjectId == subject.Id),
                    StudentId = studentId,
                    StudentName = $"{student.FirstName} {student.LastName}"
                }).ToList();

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student details.");
                ModelState.AddModelError(string.Empty, "An error occurred while loading student details. Please try again later.");
                return View(new List<StudentSubjectGradeViewModel>());
            }
        }

        // GET: Grades/AddGrade/5
        [Authorize(Roles = "Employee,Admin,Teacher")]
        public async Task<IActionResult> AddGrade(int studentId, int subjectId)
        {
            try
            {
                var student = await _studentRepository.GetByIdAsync(studentId);
                if (student == null) return new NotFoundViewResult("GradeNotFound");

                var subject = await _subjectRepository.GetByIdAsync(subjectId);
                if (subject == null) return new NotFoundViewResult("GradeNotFound");

                if (!await CanAccessStudentAndSubjectAsync(student, subjectId)) return Forbid();

                var model = new GradeViewModel
                {
                    StudentId = studentId,
                    SubjectId = subjectId,
                    StudentName = $"{student.FirstName} {student.LastName}",
                    SubjectName = subject.Name,
                    EvaluationDate = DateTime.Today
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading data to add a grade.");
                ModelState.AddModelError(string.Empty, "An error occurred while loading data to add a grade. Please try again later.");
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Grades/AddGrade
        [Authorize(Roles = "Employee,Admin,Teacher")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddGrade(GradeViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var student = await _studentRepository.GetByIdAsync(model.StudentId);
                    var subject = await _subjectRepository.GetByIdAsync(model.SubjectId);
                    if (student == null || subject == null) return new NotFoundViewResult("GradeNotFound");

                    if (!await CanAccessStudentAndSubjectAsync(student, model.SubjectId)) return Forbid();

                    var enrolledSubjects = await _gradeRepository.GetSubjectsByStudentIdAsync(model.StudentId);
                    if (!enrolledSubjects.Any(s => s.Id == model.SubjectId))
                    {
                        ModelState.AddModelError(string.Empty, "The student is not enrolled in this subject.");
                        return View(model);
                    }

                    var grade = await _converterHelper.ToGradeAsync(model, true);
                    await _gradeRepository.CreateAsync(grade);
                    return RedirectToAction(nameof(Details), new { studentId = model.StudentId });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error adding grade.");
                    ModelState.AddModelError(string.Empty, "An error occurred while adding the grade. Please try again later.");
                }
            }

            return View(model);
        }

        // GET: Grades/EditGrade/5
        [Authorize(Roles = "Employee,Admin,Teacher")]
        public async Task<IActionResult> EditGrade(int id)
        {
            try
            {
                var grade = await _gradeRepository.GetGradeWithDetailsByIdAsync(id);
                if (grade == null) return new NotFoundViewResult("GradeNotFound");

                var student = await _studentRepository.GetByIdAsync(grade.StudentId);
                if (student == null || !await CanAccessStudentAndSubjectAsync(student, grade.SubjectId)) return Forbid();

                var model = _converterHelper.ToGradeViewModel(grade);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading grade for editing.");
                ModelState.AddModelError(string.Empty, "An error occurred while loading the grade for editing. Please try again later.");
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Grades/EditGrade
        [Authorize(Roles = "Employee,Admin,Teacher")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditGrade(GradeViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var existingGrade = await _gradeRepository.GetByIdAsync(model.Id);
                    if (existingGrade == null) return new NotFoundViewResult("GradeNotFound");

                    var student = await _studentRepository.GetByIdAsync(existingGrade.StudentId);
                    if (student == null || !await CanAccessStudentAndSubjectAsync(student, existingGrade.SubjectId)) return Forbid();

                    // Lock StudentId and SubjectId to existing record to prevent ID tampering
                    model.StudentId = existingGrade.StudentId;
                    model.SubjectId = existingGrade.SubjectId;

                    var grade = await _converterHelper.ToGradeAsync(model, false);
                    await _gradeRepository.UpdateAsync(grade);
                    return RedirectToAction(nameof(Details), new { studentId = existingGrade.StudentId });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating grade.");
                    ModelState.AddModelError(string.Empty, "An error occurred while updating the grade. Please try again later.");
                }
            }

            return View(model);
        }

        // GET: Grades/DeleteGrade/5
        [Authorize(Roles = "Employee,Admin,Teacher")]
        public async Task<IActionResult> DeleteGrade(int id)
        {
            try
            {
                var grade = await _gradeRepository.GetGradeWithDetailsByIdAsync(id);
                if (grade == null) return new NotFoundViewResult("GradeNotFound");

                var student = await _studentRepository.GetByIdAsync(grade.StudentId);
                if (student == null || !await CanAccessStudentAndSubjectAsync(student, grade.SubjectId)) return Forbid();

                return View(_converterHelper.ToGradeViewModel(grade));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading grade for deletion.");
                ModelState.AddModelError(string.Empty, "An error occurred while loading the grade for deletion. Please try again later.");
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Grades/DeleteGrade
        [Authorize(Roles = "Employee,Admin,Teacher")]
        [HttpPost, ActionName("DeleteGrade")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteGradeConfirmed(int id)
        {
            try
            {
                var grade = await _gradeRepository.GetGradeWithDetailsByIdAsync(id);
                if (grade != null)
                {
                    var student = await _studentRepository.GetByIdAsync(grade.StudentId);
                    if (student == null || !await CanAccessStudentAndSubjectAsync(student, grade.SubjectId)) return Forbid();

                    await _gradeRepository.DeleteAsync(grade);
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting grade.");
                ModelState.AddModelError(string.Empty, "An error occurred while deleting the grade. Please try again later.");
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Grades/MyGrades
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MyGrades()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var studentId = await _studentRepository.GetStudentIdByUserIdAsync(userId);

                if (studentId == null)
                {
                    return new NotFoundViewResult("GradeNotFound");
                }

                var subjects = await _gradeRepository.GetSubjectsByStudentIdAsync(studentId.Value);
                var grades = await _gradeRepository.GetGradesByStudentIdAsync(studentId.Value);
                var student = await _studentRepository.GetByIdAsync(studentId.Value);

                double overallAverageGrade = grades.Any() ? grades.Average(g => g.Value) : 0;
                string overallGradeStatus = overallAverageGrade >= 9.5 ? "Passed" : "Failed";

                var model = subjects.Select(subject => new StudentSubjectGradeViewModel
                {
                    Subject = subject,
                    Grade = grades.FirstOrDefault(g => g.SubjectId == subject.Id),
                    StudentId = studentId.Value,
                    StudentName = $"{student.FirstName} {student.LastName}"
                }).ToList();

                ViewBag.OverallAverageGrade = overallAverageGrade.ToString("F2");
                ViewBag.OverallGradeStatus = overallGradeStatus;

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student's grades.");
                ModelState.AddModelError(string.Empty, "An error occurred while loading your grades. Please try again later.");
                return View(new List<StudentSubjectGradeViewModel>());
            }
        }

        public IActionResult GradeNotFound()
        {
            return View();
        }
    }
}
