using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagementSystem.Helpers;
using SchoolManagementSystem.Models;
using SchoolManagementSystem.Repositories;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SchoolManagementSystem.Controllers
{
    [Authorize(Roles = "Employee,Admin")]
    public class SchoolClassesController : Controller
    {
        private readonly ISchoolClassRepository _schoolClassRepository;
        private readonly IConverterHelper _converterHelper;
        private readonly ILogger<SchoolClassesController> _logger;

        public SchoolClassesController(
            ISchoolClassRepository schoolClassRepository,
            IConverterHelper converterHelper,
            ILogger<SchoolClassesController> logger)
        {
            _schoolClassRepository = schoolClassRepository;
            _converterHelper = converterHelper;
            _logger = logger;
        }

        // GET: SchoolClasses/Index
        public async Task<IActionResult> Index()
        {
            var schoolClasses = await _schoolClassRepository.GetAll().ToListAsync();
            var schoolClassViewModels = schoolClasses.Select(sc => _converterHelper.ToSchoolClassViewModel(sc)).ToList();
            return View(schoolClassViewModels);
        }

        // GET: SchoolClasses/Create
        public IActionResult Create()
        {
            var model = new SchoolClassViewModel();
            return View(model);
        }

        // POST: SchoolClasses/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SchoolClassViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var schoolClass = await _converterHelper.ToSchoolClassAsync(model, true);
                    await _schoolClassRepository.CreateAsync(schoolClass);
                    _logger.LogInformation("School class created successfully: {ClassName}", schoolClass.ClassName);
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating school class: {ClassName}", model.ClassName);
                    ModelState.AddModelError(string.Empty, "An unexpected error occurred while creating the class. Please try again.");
                }
            }

            return View(model);
        }

        // GET: SchoolClasses/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return new NotFoundViewResult("SchoolClassNotFound");

            var schoolClass = await _schoolClassRepository.GetByIdAsync(id.Value);
            if (schoolClass == null)
            {
                return new NotFoundViewResult("SchoolClassNotFound");
            }

            var model = _converterHelper.ToSchoolClassViewModel(schoolClass);
            return View(model);
        }

        // POST: SchoolClasses/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SchoolClassViewModel model)
        {
            if (id != model.Id) return new NotFoundViewResult("SchoolClassNotFound");

            var existingClass = await _schoolClassRepository.GetByIdAsync(id);
            if (existingClass == null)
            {
                return new NotFoundViewResult("SchoolClassNotFound");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var schoolClass = await _converterHelper.ToSchoolClassAsync(model, false);

                    // Preserve existing CourseId if not explicitly modified on the form
                    if (schoolClass.CourseId == null && existingClass.CourseId != null)
                    {
                        schoolClass.CourseId = existingClass.CourseId;
                    }

                    await _schoolClassRepository.UpdateAsync(schoolClass);
                    _logger.LogInformation("School class updated successfully: {ClassName}", schoolClass.ClassName);
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _schoolClassRepository.ExistAsync(model.Id))
                    {
                        return new NotFoundViewResult("SchoolClassNotFound");
                    }
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating school class: {ClassName}", model.ClassName);
                    ModelState.AddModelError(string.Empty, "An unexpected error occurred while updating the class. Please try again.");
                }
            }

            return View(model);
        }

        // GET: SchoolClasses/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return new NotFoundViewResult("SchoolClassNotFound");

            var schoolClass = await _schoolClassRepository.GetByIdAsync(id.Value);
            if (schoolClass == null)
            {
                return new NotFoundViewResult("SchoolClassNotFound");
            }

            var model = _converterHelper.ToSchoolClassViewModel(schoolClass);
            return View(model);
        }

        // POST: SchoolClasses/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var schoolClass = await _schoolClassRepository.GetByIdAsync(id);
            if (schoolClass == null)
            {
                return new NotFoundViewResult("SchoolClassNotFound");
            }

            try
            {
                await _schoolClassRepository.DeleteAsync(schoolClass);
                _logger.LogInformation("School class deleted successfully: {ClassId}", id);
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error deleting school class: {ClassId}", id);
                ModelState.AddModelError(string.Empty, "This class cannot be deleted because it has assigned students, teachers, or courses.");
                var model = _converterHelper.ToSchoolClassViewModel(schoolClass);
                return View("Delete", model);
            }
        }

        public IActionResult SchoolClassNotFound()
        {
            return View();
        }
    }
}
