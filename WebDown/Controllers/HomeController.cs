using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using System.Diagnostics;
using WebDown.Models;

namespace WebDown.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        private FileExtensionContentTypeProvider provider = new FileExtensionContentTypeProvider();
        public IActionResult Down()
        {
            var file = @"H:\�ٶ�����\ubuntu.zip";
            provider.TryGetContentType(file, out var contentType);
            var result = PhysicalFile(file, contentType);
            result.EnableRangeProcessing = true;
            result.FileDownloadName = Path.GetFileName(file);
            return result;
        }
    }
}
