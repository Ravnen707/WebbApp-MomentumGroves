using Microsoft.AspNetCore.Mvc;

namespace MomentumGroves.Controllers;

public class FoodController : Controller
{
    /// <summary>
    /// Displays the food tracking page.
    /// </summary>
    /// <returns></returns>
    public IActionResult Index()
    {
        return View();
    }
}