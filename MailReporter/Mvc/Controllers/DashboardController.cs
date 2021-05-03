using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusinessLogic;
using DomainModel.DTO.PRTG;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mvc.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

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

        [AllowAnonymous]
        [BasicAuth]
        public async Task<IActionResult> Prtg()
        {
            var overview = await _jobStatusService.GetOverview();

            var prtg = new PrtgResult {
                Prtg = new Prtg
                {
                    Result = overview.Select(o => new PrtgChannel
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