namespace Mvc.Controllers
{
    using System.Threading.Tasks;

    using BusinessLogic.Interfaces;

    using DomainModel;

    using Microsoft.AspNetCore.Mvc;

    public class JobController : Controller
    {
        private readonly IJobService _jobService;
        private readonly IJobExecutionService _jobStatusService;

        public JobController(IJobService jobService, IJobExecutionService jobStatusService)
        {
            _jobService = jobService;
            _jobStatusService = jobStatusService;
        }

        public async Task<IActionResult> Index()
        {
            var allJobs = await _jobService.GetAll();
            return View(allJobs);
        }

        public IActionResult Create() => View();

        [HttpPost]
        public async Task<IActionResult> Create(Job job)
        {
            if (!ModelState.IsValid)
            {
                return View(job);
            }

            await _jobService.Create(job);
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Edit(string id)
        {
            var job = await _jobService.GetById(id);
            return View(job);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Job job)
        {
            if (!ModelState.IsValid)
            {
                return View(job);
            }

            await _jobService.Update(job);
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Delete(string id)
        {
            var job = await _jobService.GetById(id);
            return View(job);
        }

        [HttpPost]
        [ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            await _jobService.Delete(id);
            return RedirectToAction("Index");
        }


        [HttpPost]
        public async Task<IActionResult> SeedJobs()
        {
            await _jobService.SeedJobs();
            return new OkResult();
        }

        [HttpPost]
        public async Task<IActionResult> MigrateData(string oldname, string newname, string newJobId)
        {
            if (string.IsNullOrWhiteSpace(oldname) ||
                string.IsNullOrWhiteSpace(newname) ||
                string.IsNullOrWhiteSpace(newJobId))
            {
                return new BadRequestResult();
            }

            await _jobStatusService.MigrateData(oldname, newname, newJobId);
            return new OkResult();
        }

        [HttpPost]
        public async Task<IActionResult> ReclassifyUnclassified()
        {
            var count = await _jobStatusService.ReclassifyUnclassified();
            return new OkObjectResult(count);
        }
    }
}