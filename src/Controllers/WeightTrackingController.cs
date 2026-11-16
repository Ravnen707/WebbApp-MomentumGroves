using Microsoft.AspNetCore.Mvc;

namespace MomentumGroves.Controllers;

public sealed class WeightTrackingController : Controller
{
    /// <summary>Displays recorded weights, their trend, and a goal forecast.</summary>
    /// <returns>The weight tracking view.</returns>
    public IActionResult Index() => View();
}
