using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagementSystem.Helpers;
using SchoolManagementSystem.Models;
using SchoolManagementSystem.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SchoolManagementSystem.Controllers
{
    [Authorize(Roles = "Employee,Admin")]
    public class CoursesController : Controller
    {
        private readonly ICourseRepository _courseRepository;
        private readonly ISchoolClassRepository _schoolClassRepository;
        private readonly ISubjectRepository _subjectRepository;
        private readonly IConverterHelper _converterHelper;
        private readonly ILogger<CoursesController> _logger;

        public CoursesController(
            ICourseRepository courseRepository,
            ISchoolClassRepository schoolClassRepository,
            ISubjectRepository subjectRepository,
            IConverterHelper converterHelper,
            ILogger<CoursesController> logger)
        {
            _courseRepository = courseRepository;
            _schoolClassRepository = schoolClassRepository;
            _subjectRepository = subjectRepository;
            _converterHelper = converterHelper;
            _logger = logger;
        }

        // GET: Courses/Index
        public async Task<IActionResult> Index()
        {
            var courseViewModels = await _courseRepository.GetAllCourseViewModelsAsync();
            return View(courseViewModels);
        }

        // GET: Courses/Create
        public async Task<IActionResult> Create()
        {
            var model = new CourseViewModel
            {
                SchoolClassItems = await GetAvailableSchoolClassItemsAsync(),
                SubjectItems = await GetAllSubjectItemsAsync()
            };

            return View(model);
        }

        // POST: Courses/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CourseViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var course = await _converterHelper.ToCourseAsync(model, true);
                    await _courseRepository.CreateAsync(course);
                    _logger.LogInformation("Course created successfully: {CourseName}", course.Name);
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating course: {CourseName}", model.Name);
                    ModelState.AddModelError(string.Empty, "An error occurred while creating the course. Please try again.");
                }
            }

            model.SchoolClassItems = await GetAvailableSchoolClassItemsAsync();
            model.SubjectItems = await GetAllSubjectItemsAsync();
            return View(model);
        }

        // GET: Courses/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return new NotFoundViewResult("CourseNotFound");

            var course = await _courseRepository.GetCourseWithDetailsAsync(id.Value);
            if (course == null)
            {
                return new NotFoundViewResult("CourseNotFound");
            }

            var model = _converterHelper.ToCourseViewModel(course);

            // Include both unassigned classes and classes currently assigned to this course
            var availableItems = await GetAvailableSchoolClassItemsAsync();
            var assignedItems = course.SchoolClasses
                .Where(sc => !availableItems.Any(a => a.Value == sc.Id.ToString()))
                .Select(sc => new SelectListItem
                {
                    Value = sc.Id.ToString(),
                    Text = sc.ClassName,
                    Selected = true
                });

            model.SchoolClassItems = availableItems.Concat(assignedItems).ToList();
            model.SubjectItems = await GetAllSubjectItemsAsync();
            model.SelectedSchoolClassIds = course.SchoolClasses.Select(sc => sc.Id).ToList();
            model.SelectedSubjectIds = course.CourseSubjects.Select(cs => cs.SubjectId).ToList();

            return View(model);
        }

        // POST: Courses/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CourseViewModel model)
        {
            if (id != model.Id) return new NotFoundViewResult("CourseNotFound");

            if (ModelState.IsValid)
            {
                try
                {
                    var course = await _converterHelper.ToCourseAsync(model, false);
                    if (course == null)
                    {
                        return new NotFoundViewResult("CourseNotFound");
                    }

                    await _courseRepository.UpdateAsync(course);
                    _logger.LogInformation("Course updated successfully: {CourseName}", course.Name);
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating course: {CourseName}", model.Name);
                    ModelState.AddModelError(string.Empty, "An error occurred while updating the course. Please try again.");
                }
            }

            model.SchoolClassItems = await GetAvailableSchoolClassItemsAsync();
            model.SubjectItems = await GetAllSubjectItemsAsync();
            return View(model);
        }

        // GET: Courses/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return new NotFoundViewResult("CourseNotFound");

            var model = await _courseRepository.GetCourseDetailsViewModelAsync(id.Value);
            if (model == null)
            {
                return new NotFoundViewResult("CourseNotFound");
            }

            return View(model);
        }

        // GET: Courses/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return new NotFoundViewResult("CourseNotFound");

            var course = await _courseRepository.GetCourseWithDetailsAsync(id.Value);
            if (course == null)
                return new NotFoundViewResult("CourseNotFound");

            var model = _converterHelper.ToCourseViewModel(course);
            return View(model);
        }

        // POST: Courses/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var course = await _courseRepository.GetByIdAsync(id);
            if (course == null)
                return new NotFoundViewResult("CourseNotFound");

            try
            {
                await _courseRepository.DeleteAsync(course);
                _logger.LogInformation("Course deleted successfully: {CourseId}", id);
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                ViewBag.ErrorTitle = $"{course.Name} cannot be deleted";
                ViewBag.ErrorMessage = "This course cannot be deleted because it has associated classes, subjects, or students.";

                if (ex.InnerException != null && ex.InnerException.Message.Contains("DELETE"))
                {
                    ViewBag.ErrorTitle = $"{course.Name} is currently in use!";
                }

                return View("Error");
            }
        }

        private async Task<List<SelectListItem>> GetAvailableSchoolClassItemsAsync()
        {
            var availableSchoolClasses = await _schoolClassRepository.GetAvailableSchoolClassesAsync();
            return availableSchoolClasses.Select(sc => new SelectListItem
            {
                Value = sc.Id.ToString(),
                Text = sc.ClassName
            }).ToList();
        }

        private async Task<List<SelectListItem>> GetAllSubjectItemsAsync()
        {
            var subjects = await _subjectRepository.GetAllSubjectsAsync();
            return subjects.Select(s => new SelectListItem
            {
                Value = s.Id.ToString(),
                Text = s.Name
            }).ToList();
        }

        public IActionResult CourseNotFound()
        {
            return View();
        }
    }
}
