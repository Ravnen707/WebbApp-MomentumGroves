using Microsoft.AspNetCore.Mvc;

namespace MomentumGroves.Controllers;

public class TrainingController : Controller
{
    /// <summary>
    /// Displays the training page.
    /// </summary>
    /// <returns></returns>
    public IActionResult Index() => View();
}