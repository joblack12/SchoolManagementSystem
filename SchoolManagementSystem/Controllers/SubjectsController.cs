using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Helpers;
using SchoolManagementSystem.Models;
using SchoolManagementSystem.Repositories;
using System.Linq;
using System.Threading.Tasks;

namespace SchoolManagementSystem.Controllers
{
    [Authorize(Roles = "Employee,Admin")]
    public class SubjectsController : Controller
    {
        private readonly ISubjectRepository _subjectRepository;
        private readonly IConverterHelper _converterHelper;

        public SubjectsController(ISubjectRepository subjectRepository, IConverterHelper converterHelper)
        {
            _subjectRepository = subjectRepository;
            _converterHelper = converterHelper;
        }

        // GET: Subjects/Index
        public async Task<IActionResult> Index()
        {
            var subjects = await _subjectRepository.GetAll().ToListAsync();
            var subjectViewModels = subjects.Select(s => _converterHelper.ToSubjectViewModel(s)).ToList();
            return View(subjectViewModels);
        }

        // GET: Subjects/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Subjects/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SubjectViewModel model)
        {
            if (ModelState.IsValid)
            {
                var subject = await _converterHelper.ToSubjectAsync(model, true);
                await _subjectRepository.CreateAsync(subject);
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        // GET: Subjects/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return new NotFoundViewResult("SubjectNotFound");

            var subject = await _subjectRepository.GetByIdAsync(id.Value);
            if (subject == null)
            {
                return new NotFoundViewResult("SubjectNotFound");
            }

            var model = _converterHelper.ToSubjectViewModel(subject);
            return View(model);
        }

        // POST: Subjects/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SubjectViewModel model)
        {
            if (id != model.Id) return new NotFoundViewResult("SubjectNotFound");

            if (!await _subjectRepository.ExistAsync(id))
            {
                return new NotFoundViewResult("SubjectNotFound");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var subject = await _converterHelper.ToSubjectAsync(model, false);
                    await _subjectRepository.UpdateAsync(subject);
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _subjectRepository.ExistAsync(model.Id))
                    {
                        return new NotFoundViewResult("SubjectNotFound");
                    }
                    throw;
                }
            }

            return View(model);
        }

        // GET: Subjects/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return new NotFoundViewResult("SubjectNotFound");

            var subject = await _subjectRepository.GetByIdAsync(id.Value);
            if (subject == null)
            {
                return new NotFoundViewResult("SubjectNotFound");
            }

            var model = _converterHelper.ToSubjectViewModel(subject);
            return View(model);
        }

        // POST: Subjects/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var subject = await _subjectRepository.GetByIdAsync(id);
            if (subject == null)
            {
                return new NotFoundViewResult("SubjectNotFound");
            }

            try
            {
                await _subjectRepository.DeleteAsync(subject);
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                ViewBag.ErrorTitle = $"{subject.Name} cannot be deleted";
                ViewBag.ErrorMessage = "This subject cannot be deleted because it has associated courses, teachers, grades, or attendance records.";

                if (ex.InnerException != null && ex.InnerException.Message.Contains("DELETE"))
                {
                    ViewBag.ErrorTitle = $"{subject.Name} is being used!";
                }

                return View("Error");
            }
        }

        public IActionResult SubjectNotFound()
        {
            return View();
        }
    }
}
