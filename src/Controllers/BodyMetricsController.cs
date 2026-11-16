using Microsoft.AspNetCore.Mvc;

namespace MomentumGroves.Controllers;

public sealed class BodyMetricsController : Controller
{
    /// <summary>Displays the BMI-related inputs and nutrition target calculator.</summary>
    /// <returns>The body metrics view.</returns>
    public IActionResult Index() => View();
}
