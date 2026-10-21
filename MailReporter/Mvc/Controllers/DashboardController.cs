namespace Mvc.Controllers
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using BusinessLogic.Interfaces;

    using DomainModel.DTO.PRTG;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    using Mvc.Attributes;
    using Mvc.Models;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    public class DashboardController : Controller
    {
        private readonly IJobExecutionService _jobStatusService;
        private readonly IFunctionHealthService _functionHealthService;

        public DashboardController(IJobExecutionService jobStatusService, IFunctionHealthService functionHealthService)
        {
            _jobStatusService = jobStatusService;
            _functionHealthService = functionHealthService;
        }

        public async Task<IActionResult> Index()
        {
            var model = new DashboardViewModel
            {
                Jobs = await _jobStatusService.GetOverview(),
                Functions = await _functionHealthService.GetOverview(),
            };
            return View(model);
        }

        [AllowAnonymous]
        [BasicAuth]
        public async Task<IActionResult> Prtg()
        {
            var overview = await _jobStatusService.GetOverview();

            var prtg = new PrtgResult
            {
                Prtg = new Prtg
                {
                    Result = overview.Where(o => o.LastRun != null).Select(o => new PrtgChannel
                    {
                        Channel = o.Job.Name,
                        Value = (int)((o.LastRun + o.Job.ExpectedInterval) - DateTime.UtcNow)?.TotalSeconds,
                        Unit = "TimeSeconds",
                        Warning = o.IsDue ? 1 : 0,
                        LimitMinWarning = 0,
                        LimitMinError = -(int)(o.Job.ExpectedInterval.TotalSeconds * 0.1),
                        LimitMode = 1
                    }).ToList()
                }
            };

            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy()
                }
            };

            return new JsonResult(prtg, serializerSettings);
        }
    }
}