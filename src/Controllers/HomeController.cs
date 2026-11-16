using Microsoft.AspNetCore.Mvc;

namespace MomentumGroves.Controllers;

public class HomeController : Controller
{
    /// <summary>
    /// Displays the home page.
    /// </summary>
    /// <returns></returns>
    public IActionResult Index() => View();
}