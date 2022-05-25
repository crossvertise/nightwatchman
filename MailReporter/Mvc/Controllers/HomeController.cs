namespace Mvc.Controllers
{
    using System.Diagnostics;

    using Microsoft.AspNetCore.Mvc;

    using Mvc.Models;

    public class HomeController : Controller
    {
        public IActionResult Index() => RedirectToAction("Index", "Dashboard");

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
