using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MomentumGroves.Repository;
using MomentumGroves.Models;
using MomentumGroves.Service;

namespace MomentumGroves.Controllers;

[Route("routes")]
public sealed class RoutesController : Controller
{
    private readonly RouteService _service;
    private readonly RouteRepository _repository;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes the routes controller.
    /// </summary>
    /// <param name="service"></param>
    /// <param name="repository"></param>
    /// <param name="configuration"></param>
    public RoutesController(RouteService service, RouteRepository repository, IConfiguration configuration)
    {
        _service = service;
        _repository = repository;
        _configuration = configuration;
    }

    /// <summary>
    /// Displays the route planner with its map configuration.
    /// </summary>
    /// <returns></returns>
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewBag.TracestrackKey = _configuration["Tracestrack:ApiKey"] ?? "";
        ViewBag.OsmTileUrl = _configuration["Routes:OsmTileUrl"] ?? "https://tile.openstreetmap.org/{z}/{x}/{y}.png";
        ViewBag.TracestrackTileUrl = _configuration["Routes:TracestrackTileUrl"] ??
                                     "https://tile.tracestrack.com/topo__/{z}/{x}/{y}.png";
        return View();
    }

    /// <summary>
    /// Searches for an area or address and handles validation and upstream errors.
    /// </summary>
    /// <param name="query"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 180)
        {
            return BadRequest(new { error = "Angiv et område eller en adresse." });
        }

        try
        {
            return Content(await _service.SearchAsync(query, cancellationToken), "application/json");
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new { error = "Adressesøgningen er midlertidigt utilgængelig." });
        }
    }

    /// <summary>
    /// Generates a route and handles invalid settings or route service errors.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost("generate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate([FromBody] RouteGenerateRequest request,
        CancellationToken cancellationToken)
    {
        if (!ValidCoordinates(request.Latitude, request.Longitude) || request.DistanceKm < 0.5 ||
            request.DistanceKm > 40 || !ValidActivity(request.Activity) || !ValidEnvironment(request.Environment))
        {
            return BadRequest(new { error = "Ugyldige ruteindstillinger." });
        }

        try
        {
            return Json(await _service.GenerateAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new { error = "Ruteberegningen er midlertidigt utilgængelig." });
        }
    }

    /// <summary>
    /// Returns the saved routes.
    /// </summary>
    /// <returns></returns>
    [HttpGet("saved")]
    public IActionResult Saved() => Json(_repository.List());

    /// <summary>
    /// Validates route details and geometry before saving a route.
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpPost("saved")]
    [ValidateAntiForgeryToken]
    public IActionResult Save([FromBody] RouteSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100 ||
            !ValidCoordinates(request.Latitude, request.Longitude) || request.DistanceKm <= 0 ||
            request.TargetDistanceKm < 0.5 || request.TargetDistanceKm > 40 || !ValidActivity(request.Activity) ||
            !ValidEnvironment(request.Environment) || string.IsNullOrWhiteSpace(request.GeoJson) ||
            request.GeoJson.Length > 200000)
        {
            return BadRequest(new { error = "Ugyldige rutedata." });
        }

        try
        {
            using var json = JsonDocument.Parse(request.GeoJson);
            if (json.RootElement.GetProperty("type").GetString() != "LineString" ||
                json.RootElement.GetProperty("coordinates").GetArrayLength() < 2)
            {
                return BadRequest(new { error = "Ugyldig rutegeometri." });
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return BadRequest(new { error = "Ugyldig rutegeometri." });
        }

        return Json(_repository.Save(request));
    }

    /// <summary>
    /// Deletes a saved route or reports that it was not found.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpDelete("saved/{id:long}")]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(long id)
    {
        return _repository.Delete(id) ? NoContent() : NotFound();
    }

    /// <summary>
    /// Determines whether coordinates are finite and within geographic bounds.
    /// </summary>
    /// <param name="lat"></param>
    /// <param name="lon"></param>
    /// <returns></returns>
    private static bool ValidCoordinates(double lat, double lon) => double.IsFinite(lat) && double.IsFinite(lon) &&
                                                                    lat >= -90 && lat <= 90 && lon >= -180 &&
                                                                    lon <= 180;

    /// <summary>
    /// Determines whether the activity is walking or running.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    private static bool ValidActivity(string value) => value is "walk" or "run";

    /// <summary>
    /// Determines whether the environment is city, outside, or mixed.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    private static bool ValidEnvironment(string value) => value is "city" or "outside" or "mixed";
}