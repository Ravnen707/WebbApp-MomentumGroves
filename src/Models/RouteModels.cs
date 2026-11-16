namespace MomentumGroves.Models;

public sealed record RouteGenerateRequest(double Latitude, double Longitude, double DistanceKm, string Environment, string Activity);
public sealed record RouteSaveRequest(string Name, string Activity, string Environment, double TargetDistanceKm, double DistanceKm, double Latitude, double Longitude, string GeoJson);
public sealed record SavedRoute(long Id, string Name, string Activity, string Environment, double TargetDistanceKm, double DistanceKm, double Latitude, double Longitude, string GeoJson, string CreatedUtc);
public sealed record RouteCandidate(double DistanceKm, string GeoJson);
