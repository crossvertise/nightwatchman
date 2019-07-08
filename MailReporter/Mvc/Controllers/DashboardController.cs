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
        private IJobStatusService _jobStatusService;

        public DashboardController(IJobStatusService jobStatusService)
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