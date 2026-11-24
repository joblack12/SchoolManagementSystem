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
    public class AttendanceController : Controller
    {
        private readonly IAttendanceRepository _attendanceRepository;
        private readonly IStudentRepository _studentRepository;
        private readonly ISubjectRepository _subjectRepository;
        private readonly ITeacherRepository _teacherRepository;
        private readonly IConverterHelper _converterHelper;
        private readonly ISchoolClassRepository _schoolClassRepository;
        private readonly ILogger<AttendanceController> _logger;

        public AttendanceController(
            IAttendanceRepository attendanceRepository,
            IStudentRepository studentRepository,
            ISubjectRepository subjectRepository,
            ITeacherRepository teacherRepository,
            IConverterHelper converterHelper,
            ISchoolClassRepository schoolClassRepository,
            ILogger<AttendanceController> logger)
        {
            _attendanceRepository = attendanceRepository;
            _studentRepository = studentRepository;
            _subjectRepository = subjectRepository;
            _teacherRepository = teacherRepository;
            _converterHelper = converterHelper;
            _schoolClassRepository = schoolClassRepository;
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

        [Authorize(Roles = "Employee,Admin,Teacher")]
        public async Task<IActionResult> Index(int? classId)
        {
            try
            {
                var schoolClasses = await _schoolClassRepository.GetAllAsync();

                // Scope dropdown classes for Teachers
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
                    var studentAttendances = new List<StudentAttendanceViewModel>();

                    foreach (var student in students)
                    {
                        var subjectsForStudent = await _subjectRepository.GetSubjectsByStudentIdAsync(student.Id);
                        int totalClasses = subjectsForStudent.Sum(s => s.TotalClasses);
                        var attendances = await _attendanceRepository.GetAttendancesByStudentIdAsync(student.Id);

                        studentAttendances.Add(new StudentAttendanceViewModel
                        {
                            Student = student,
                            Attendances = attendances,
                            TotalClasses = totalClasses
                        });
                    }

                    return View(studentAttendances);
                }

                return View(new List<StudentAttendanceViewModel>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading attendance list.");
                ModelState.AddModelError(string.Empty, "An error occurred while loading the attendance list. Please try again later.");
                return View(new List<StudentAttendanceViewModel>());
            }
        }

        // GET: Attendance/Details/5
        [Authorize(Roles = "Employee,Admin,Teacher")]
        public async Task<IActionResult> Details(int studentId, int classId)
        {
            try
            {
                var student = await _studentRepository.GetByIdAsync(studentId);
                if (student == null) return new NotFoundViewResult("StudentNotFound");

                if (!await CanAccessStudentAndSubjectAsync(student)) return Forbid();

                var subjects = await _attendanceRepository.GetSubjectsByStudentIdAsync(studentId);
                var attendances = await _attendanceRepository.GetAttendancesByStudentIdAsync(studentId);

                // Filter subjects if user is strictly a Teacher
                if (User.IsInRole("Teacher") && !User.IsInRole("Admin") && !User.IsInRole("Employee"))
                {
                    var teacher = await GetCurrentTeacherWithDetailsAsync();
                    var allowedSubjectIds = teacher?.TeacherSubjects.Select(ts => ts.SubjectId).ToHashSet() ?? new HashSet<int>();
                    subjects = subjects.Where(s => allowedSubjectIds.Contains(s.Id)).ToList();
                }

                var model = subjects.Select(subject => new StudentSubjectAttendanceViewModel
                {
                    Subject = subject,
                    Attendance = attendances.FirstOrDefault(a => a.SubjectId == subject.Id),
                    StudentId = studentId,
                    StudentName = $"{student.FirstName} {student.LastName}",
                    AllAttendances = attendances.Where(a => a.SubjectId == subject.Id).ToList(),
                    CanAddAttendance = (attendances.Count(a => a.SubjectId == subject.Id) < subject.TotalClasses)
                }).ToList();

                ViewBag.ClassId = classId != 0 ? classId : (student.SchoolClassId ?? 0);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading attendance details.");
                ModelState.AddModelError(string.Empty, "An error occurred while loading attendance details. Please try again later.");
                return View(new List<StudentSubjectAttendanceViewModel>());
            }
        }

        // GET: Attendance/AddAttendance
        [Authorize(Roles = "Employee,Admin,Teacher")]
        public async Task<IActionResult> AddAttendance(int studentId, int subjectId)
        {
            try
            {
                var student = await _studentRepository.GetByIdAsync(studentId);
                var subject = await _subjectRepository.GetByIdAsync(subjectId);

                if (student == null || subject == null)
                {
                    return new NotFoundViewResult("StudentOrSubjectNotFound");
                }

                if (!await CanAccessStudentAndSubjectAsync(student, subjectId)) return Forbid();

                var model = new AttendanceViewModel
                {
                    StudentId = studentId,
                    SubjectId = subjectId,
                    StudentName = $"{student.FirstName} {student.LastName}",
                    SubjectName = subject.Name,
                    Date = DateTime.Today
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading data to add attendance.");
                ModelState.AddModelError(string.Empty, "An error occurred while loading data to add attendance. Please try again later.");
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Attendance/AddAttendance
        [Authorize(Roles = "Employee,Admin,Teacher")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAttendance(AttendanceViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var student = await _studentRepository.GetByIdAsync(model.StudentId);
                    var subject = await _subjectRepository.GetByIdAsync(model.SubjectId);
                    if (student == null || subject == null)
                        if (student == null || subject == null)
                        {
                            return new NotFoundViewResult("StudentOrSubjectNotFound");
                        }

                    if (!await CanAccessStudentAndSubjectAsync(student, model.SubjectId)) return Forbid();

                    // Verify student is enrolled in this subject and hasn't reached TotalClasses cap
                    var enrolledSubjects = await _subjectRepository.GetSubjectsByStudentIdAsync(model.StudentId);
                    if (!enrolledSubjects.Any(s => s.Id == model.SubjectId))
                    {
                        ModelState.AddModelError(string.Empty, "The student is not enrolled in this subject.");
                        return View(model);
                    }

                    var existingAttendances = await _attendanceRepository.GetAttendancesByStudentIdAsync(model.StudentId);
                    if (existingAttendances.Count(a => a.SubjectId == model.SubjectId) >= subject.TotalClasses)
                    {
                        ModelState.AddModelError(string.Empty, $"Cannot record more absences than total classes ({subject.TotalClasses}).");
                        return View(model);
                    }

                    var attendance = await _converterHelper.ToAttendanceAsync(model, true);
                    await _attendanceRepository.AddAttendanceAsync(attendance);
                    return RedirectToAction(nameof(Details), new { studentId = model.StudentId, classId = student.SchoolClassId ?? 0 });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error adding attendance.");
                    ModelState.AddModelError(string.Empty, "An error occurred while adding the attendance. Please try again later.");
                }
            }

            return View(model);
        }

        // GET: Attendance/EditAttendance/5
        [Authorize(Roles = "Employee,Admin,Teacher")]
        public async Task<IActionResult> EditAttendance(int id)
        {
            try
            {
                var attendance = await _attendanceRepository.GetAttendanceWithDetailsByIdAsync(id);
                if (attendance == null) return new NotFoundViewResult("AttendanceNotFound");

                var student = await _studentRepository.GetByIdAsync(attendance.StudentId);
                if (student == null || !await CanAccessStudentAndSubjectAsync(student, attendance.SubjectId)) return Forbid();

                var model = _converterHelper.ToAttendanceViewModel(attendance);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading attendance for editing.");
                ModelState.AddModelError(string.Empty, "An error occurred while loading the attendance for editing. Please try again later.");
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Attendance/EditAttendance
        [Authorize(Roles = "Employee,Admin,Teacher")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAttendance(AttendanceViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var existingAttendance = await _attendanceRepository.GetByIdAsync(model.Id);
                    if (existingAttendance == null) return new NotFoundViewResult("AttendanceNotFound");

                    var student = await _studentRepository.GetByIdAsync(existingAttendance.StudentId);
                    if (student == null || !await CanAccessStudentAndSubjectAsync(student, existingAttendance.SubjectId)) return Forbid();

                    // Lock StudentId and SubjectId to existing record to prevent ID tampering
                    model.StudentId = existingAttendance.StudentId;
                    model.SubjectId = existingAttendance.SubjectId;

                    var attendance = await _converterHelper.ToAttendanceAsync(model, false);
                    await _attendanceRepository.UpdateAsync(attendance);
                    return RedirectToAction(nameof(Details), new { studentId = existingAttendance.StudentId, classId = student.SchoolClassId ?? 0 });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating attendance.");
                    ModelState.AddModelError(string.Empty, "An error occurred while updating the attendance. Please try again later.");
                }
            }

            return View(model);
        }

        // GET: Attendance/DeleteAttendance/5
        [Authorize(Roles = "Employee,Admin,Teacher")]
        public async Task<IActionResult> DeleteAttendance(int id)
        {
            try
            {
                var attendance = await _attendanceRepository.GetAttendanceWithDetailsByIdAsync(id);
                if (attendance == null) return new NotFoundViewResult("AttendanceNotFound");

                var student = await _studentRepository.GetByIdAsync(attendance.StudentId);
                if (student == null || !await CanAccessStudentAndSubjectAsync(student, attendance.SubjectId)) return Forbid();

                return View(_converterHelper.ToAttendanceViewModel(attendance));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading attendance for deletion.");
                ModelState.AddModelError(string.Empty, "An error occurred while loading the attendance for deletion. Please try again later.");
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Attendance/DeleteAttendance
        [Authorize(Roles = "Employee,Admin,Teacher")]
        [HttpPost, ActionName("DeleteAttendance")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAttendanceConfirmed(int id)
        {
            try
            {
                var attendance = await _attendanceRepository.GetAttendanceWithDetailsByIdAsync(id);
                if (attendance != null)
                {
                    var student = await _studentRepository.GetByIdAsync(attendance.StudentId);
                    if (student == null || !await CanAccessStudentAndSubjectAsync(student, attendance.SubjectId)) return Forbid();

                    await _attendanceRepository.DeleteAsync(attendance);
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting attendance.");
                ModelState.AddModelError(string.Empty, "An error occurred while deleting the attendance. Please try again later.");
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Attendance/MyAttendances
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MyAttendances()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var studentId = await _studentRepository.GetStudentIdByUserIdAsync(userId);

                if (studentId == null)
                {
                    return new NotFoundViewResult("StudentNotFound");
                }

                var subjects = await _subjectRepository.GetSubjectsByStudentIdAsync(studentId.Value);
                var attendances = await _attendanceRepository.GetAttendancesByStudentIdAsync(studentId.Value);
                var student = await _studentRepository.GetByIdAsync(studentId.Value);

                int totalClasses = subjects.Sum(s => s.TotalClasses);
                int totalAbsences = attendances.Count;
                string overallStatus = totalAbsences >= totalClasses * 0.3 ? "Failed" : "Passed";

                var model = subjects.Select(subject => new StudentSubjectAttendanceViewModel
                {
                    Subject = subject,
                    AllAttendances = attendances.Where(a => a.SubjectId == subject.Id).ToList(),
                    StudentId = studentId.Value,
                    StudentName = $"{student.FirstName} {student.LastName}"
                }).ToList();

                ViewBag.TotalClasses = totalClasses;
                ViewBag.TotalAbsences = totalAbsences;
                ViewBag.OverallAttendanceStatus = overallStatus;

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student's attendances.");
                ModelState.AddModelError(string.Empty, "An error occurred while loading your attendances. Please try again later.");
                return View(new List<StudentSubjectAttendanceViewModel>());
            }
        }

        public IActionResult StudentNotFound() => View();
        public IActionResult AttendanceNotFound() => View();
        public IActionResult StudentOrSubjectNotFound() => View();
    }
}
