using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusinessLogic;
using Microsoft.AspNetCore.Mvc;

namespace Mvc.Controllers
{
    public class DashboardController : Controller
    {
        private IJobExecutionService _jobStatusService;

        public DashboardController(IJobExecutionService jobStatusService)
        {
            _jobStatusService = jobStatusService;
        }

        public async Task<IActionResult> Index()
        {
            var overview = await _jobStatusService.GetOverview();

            return View(overview);
        }
    }
}