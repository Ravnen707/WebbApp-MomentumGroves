using Microsoft.Data.Sqlite;
using MomentumGroves.Models;

namespace MomentumGroves.Repository;

/// <summary>
/// Persists explicitly saved routes in the existing application database.
/// </summary>
public sealed class RouteRepository
{
    private readonly string _connectionString;

    public RouteRepository(IWebHostEnvironment environment)
    {
        var path = Path.Combine(environment.ContentRootPath, "src", "Data", "momentum.db");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS SavedRoutes (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL, Activity TEXT NOT NULL, Environment TEXT NOT NULL,
                TargetDistanceKm REAL NOT NULL, DistanceKm REAL NOT NULL,
                Latitude REAL NOT NULL, Longitude REAL NOT NULL,
                GeoJson TEXT NOT NULL, CreatedUtc TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public IReadOnlyList<SavedRoute> List()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Activity, Environment, TargetDistanceKm, DistanceKm, Latitude, Longitude, GeoJson, CreatedUtc FROM SavedRoutes ORDER BY Id DESC";
        using var reader = command.ExecuteReader();
        var routes = new List<SavedRoute>();
        while (reader.Read())
        {
            routes.Add(Read(reader));
        }
        return routes;
    }

    public SavedRoute Save(RouteSaveRequest route)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO SavedRoutes (Name, Activity, Environment, TargetDistanceKm, DistanceKm, Latitude, Longitude, GeoJson, CreatedUtc)
            VALUES ($name, $activity, $environment, $target, $distance, $lat, $lon, $geo, $created);
            SELECT last_insert_rowid();
            """;
        var created = DateTimeOffset.UtcNow.ToString("O");
        command.Parameters.AddWithValue("$name", route.Name.Trim());
        command.Parameters.AddWithValue("$activity", route.Activity);
        command.Parameters.AddWithValue("$environment", route.Environment);
        command.Parameters.AddWithValue("$target", route.TargetDistanceKm);
        command.Parameters.AddWithValue("$distance", route.DistanceKm);
        command.Parameters.AddWithValue("$lat", route.Latitude);
        command.Parameters.AddWithValue("$lon", route.Longitude);
        command.Parameters.AddWithValue("$geo", route.GeoJson);
        command.Parameters.AddWithValue("$created", created);
        var id = (long)(command.ExecuteScalar() ?? 0L);
        return new SavedRoute(id, route.Name.Trim(), route.Activity, route.Environment, route.TargetDistanceKm, route.DistanceKm, route.Latitude, route.Longitude, route.GeoJson, created);
    }

    public bool Delete(long id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM SavedRoutes WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);
        return command.ExecuteNonQuery() > 0;
    }

    private static SavedRoute Read(SqliteDataReader reader)
    {
        return new SavedRoute(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetDouble(4), reader.GetDouble(5), reader.GetDouble(6), reader.GetDouble(7), reader.GetString(8), reader.GetString(9));
    }
}
