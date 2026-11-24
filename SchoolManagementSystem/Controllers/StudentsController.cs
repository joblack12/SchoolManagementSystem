using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SchoolManagementSystem.Models;
using SchoolManagementSystem.Repositories;
using SchoolManagementSystem.Helpers;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Data.Entities;
using Microsoft.AspNetCore.Authorization;

namespace SchoolManagementSystem.Controllers
{
    [Authorize(Roles = "Employee,Admin")]
    public class StudentsController : Controller
    {
        private readonly IStudentRepository _studentRepository;
        private readonly IBlobHelper _blobHelper;
        private readonly IConverterHelper _converterHelper;
        private readonly ISchoolClassRepository _schoolClassRepository;
        private readonly IUserHelper _userHelper;
        private readonly ILogger<StudentsController> _logger;

        public StudentsController(
            IStudentRepository studentRepository,
            IBlobHelper blobHelper,
            IConverterHelper converterHelper,
            ISchoolClassRepository schoolClassRepository,
            IUserHelper userHelper,
            ILogger<StudentsController> logger)
        {
            _studentRepository = studentRepository;
            _blobHelper = blobHelper;
            _converterHelper = converterHelper;
            _schoolClassRepository = schoolClassRepository;
            _userHelper = userHelper;
            _logger = logger;
        }

        // GET: Students
        public async Task<IActionResult> Index()
        {
            var students = await _studentRepository.GetAllWithIncludesAsync();
            return View(students);
        }

        // GET: Students/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return new NotFoundViewResult("StudentNotFound");

            var student = await _studentRepository.GetStudentWithCoursesAsync(id.Value);
            if (student == null)
                return new NotFoundViewResult("StudentNotFound");

            return View(_converterHelper.ToStudentViewModel(student));
        }

        // GET: Students/Create
        public async Task<IActionResult> Create()
        {
            var pendingUsers = await _userHelper.GetAllUsersInRoleAsync("Pending");

            var model = new StudentViewModel
            {
                PendingUsers = pendingUsers
            };

            ViewBag.PendingUsers = new SelectList(pendingUsers, "Id", "Email");
            await LoadDropdownData();

            return View(model);
        }

        // POST: Students/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StudentViewModel model)
        {
            var pendingUsers = await _userHelper.GetAllUsersInRoleAsync("Pending");

            if (ModelState.IsValid)
            {
                // Verify target UserId is strictly in the Pending role
                var user = pendingUsers.FirstOrDefault(u => u.Id == model.UserId);
                if (user == null)
                {
                    ModelState.AddModelError(nameof(model.UserId), "The selected user is invalid or is no longer pending approval.");
                    model.PendingUsers = pendingUsers;
                    ViewBag.PendingUsers = new SelectList(pendingUsers, "Id", "Email");
                    await LoadDropdownData();
                    return View(model);
                }

                // Checks if the selected SchoolClass has an associated CourseId
                if (model.SchoolClassId.HasValue)
                {
                    var schoolClass = await _schoolClassRepository.GetByIdAsync(model.SchoolClassId.Value);
                    if (schoolClass == null || schoolClass.CourseId == null)
                    {
                        TempData["ErrorMessage"] = "The selected school class does not have an associated course.";
                        ModelState.AddModelError("SchoolClassId", "The selected school class does not have an associated course.");
                        model.PendingUsers = pendingUsers;
                        ViewBag.PendingUsers = new SelectList(pendingUsers, "Id", "Email");
                        await LoadDropdownData();
                        return View(model);
                    }
                }

                try
                {
                    Guid imageId = Guid.Empty;
                    if (model.ImageFile != null && model.ImageFile.Length > 0)
                    {
                        imageId = await _blobHelper.UploadBlobAsync(model.ImageFile, "students");
                    }

                    var student = await _converterHelper.ToStudentAsync(model, imageId, true);
                    await _studentRepository.CreateAsync(student);

                    await _userHelper.RemoveUserFromRoleAsync(user, "Pending");
                    await _userHelper.AddUserToRoleAsync(user, "Student");

                    TempData["SuccessMessage"] = "Student created successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError(nameof(model.ImageFile), ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating student"); // or "Error updating student" in Edit
                    ModelState.AddModelError("", "An unexpected error occurred. Please try again.");
                }

            }

            // Reload dropdowns in case of error
            model.PendingUsers = pendingUsers;
            ViewBag.PendingUsers = new SelectList(pendingUsers, "Id", "Email");
            await LoadDropdownData();
            return View(model);
        }

        // GET: Students/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return new NotFoundViewResult("StudentNotFound");

            var student = await _studentRepository.GetByIdAsync(id.Value);
            if (student == null) return new NotFoundViewResult("StudentNotFound");

            var model = _converterHelper.ToStudentViewModel(student);
            await LoadDropdownData();

            return View(model);
        }

        // POST: Students/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, StudentViewModel model)
        {
            if (id != model.Id) return new NotFoundViewResult("StudentNotFound");

            var existingStudent = await _studentRepository.GetByIdAsync(id);
            if (existingStudent == null) return new NotFoundViewResult("StudentNotFound");

            // Lock UserId to the trusted database entity to prevent UserId tampering
            model.UserId = existingStudent.UserId;

            if (ModelState.IsValid)
            {
                if (model.SchoolClassId.HasValue)
                {
                    var schoolClass = await _schoolClassRepository.GetByIdAsync(model.SchoolClassId.Value);
                    if (schoolClass == null || schoolClass.CourseId == null)
                    {
                        TempData["ErrorMessage"] = "The selected school class does not have an associated course.";
                        ModelState.AddModelError("SchoolClassId", "The selected school class does not have an associated course.");
                        await LoadDropdownData();
                        return View(model);
                    }
                }

                try
                {
                    Guid imageId = existingStudent.ImageId;

                    if (model.ImageFile != null && model.ImageFile.Length > 0)
                    {
                        imageId = await _blobHelper.UploadBlobAsync(model.ImageFile, "students");
                    }

                    var student = await _converterHelper.ToStudentAsync(model, imageId, false);
                    await _studentRepository.UpdateAsync(student);

                    // Update associated user data using trusted existingStudent.UserId
                    var user = await _userHelper.GetUserByIdAsync(existingStudent.UserId);
                    if (user != null)
                    {
                        user.FirstName = model.FirstName;
                        user.LastName = model.LastName;
                        await _userHelper.UpdateUserAsync(user);
                    }

                    TempData["SuccessMessage"] = "Student updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await StudentExists(model.Id)) return new NotFoundViewResult("StudentNotFound");
                    throw;
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError(nameof(model.ImageFile), ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating student"); // or "Error updating student" in Edit
                    ModelState.AddModelError("", "An unexpected error occurred. Please try again.");
                }
            }

            await LoadDropdownData();
            return View(model);
        }

        // GET: Students/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return new NotFoundViewResult("StudentNotFound");

            var student = await _studentRepository.GetStudentWithCoursesAsync(id.Value);
            if (student == null) return new NotFoundViewResult("StudentNotFound");

            return View(_converterHelper.ToStudentViewModel(student));
        }

        // POST: Students/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var student = await _studentRepository.GetByIdAsync(id);
            if (student == null) return new NotFoundViewResult("StudentNotFound");

            try
            {
                await _studentRepository.DeleteAsync(student);
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                ViewBag.ErrorTitle = $"{student.FirstName} {student.LastName} cannot be deleted";
                ViewBag.ErrorMessage = "This student cannot be deleted because they have associated records (such as grades or attendance).";

                if (ex.InnerException != null && ex.InnerException.Message.Contains("DELETE"))
                {
                    ViewBag.ErrorTitle = $"{student.FirstName} {student.LastName} is currently in use!";
                }

                return View("Error");
            }
        }

        private async Task<bool> StudentExists(int id)
        {
            return await _studentRepository.ExistAsync(id);
        }

        private async Task LoadDropdownData()
        {
            var schoolClasses = await _schoolClassRepository.GetAllAsync();
            var classList = schoolClasses.ToList();

            ViewBag.SchoolClassId = new SelectList(classList, "Id", "ClassName");
        }

        public IActionResult StudentNotFound()
        {
            return View();
        }
    }
}
