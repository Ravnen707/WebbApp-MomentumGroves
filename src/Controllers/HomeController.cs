using Microsoft.AspNetCore.Mvc;

namespace MomentumGroves.Controllers;

public class HomeController : Controller
{
    /// <summary>
    /// Displays the home page.
    /// </summary>
    /// <returns>The requested page view.</returns>
    public IActionResult Index()
    {
        return View();
    }
}
