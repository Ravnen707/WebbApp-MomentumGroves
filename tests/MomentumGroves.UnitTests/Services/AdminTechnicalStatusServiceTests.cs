using MomentumGroves.Data;
using MomentumGroves.Data.TrainingImages;
using MomentumGroves.Models;
using MomentumGroves.Service;

namespace MomentumGroves.Tests.Services;

[TestFixture]
public sealed class AdminTechnicalStatusServiceTests : DatabaseServiceTests
{
    /// <summary>
    /// Checks that missing or blank Supabase configuration reports standalone storage.
    /// </summary>
    /// <param name="connectionString">The missing or blank Supabase connection string.</param>
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void GetStatus_NoSupabaseConnection_ReportsStandaloneStorage(string? connectionString)
    {
        using var database = new PersonalDatabase(DataPath(""), connectionString, mobileDataApiEnabled: true);
        var service = new AdminTechnicalStatusService(database, new TrainingImageService(Environment));

        Assert.That(service.GetStatus(), Is.EqualTo(new AdminTechnicalStatus(
            "Standalone", "SQLite", "Local folder (wwwroot/TrainingImages)", "Local SQLite (frida.db)", "Disabled")));
    }

    /// <summary>
    /// Checks that Supabase storage is reported alongside the configured Mobile Data API setting.
    /// </summary>
    /// <param name="mobileDataApiEnabled">Whether the Mobile Data API is enabled in configuration.</param>
    /// <param name="expectedApiStatus">The expected Mobile Data API label.</param>
    [TestCase(true, "Enabled")]
    [TestCase(false, "Disabled")]
    public void GetStatus_SupabaseConfigured_ReportsCloudStorage(bool mobileDataApiEnabled, string expectedApiStatus)
    {
        using var database = new PersonalDatabase(DataPath(""), "Host=database.example.test", mobileDataApiEnabled);
        using var client = new HttpClient();
        var cloudStore = new SupabaseTrainingImageStore(client, TestHttp.Configuration());
        var service = new AdminTechnicalStatusService(database, new TrainingImageService(Environment, cloudStore));

        Assert.That(service.GetStatus(), Is.EqualTo(new AdminTechnicalStatus(
            "Supabase", "Supabase PostgreSQL", "Supabase Storage (private bucket)", "Local SQLite (frida.db)",
            expectedApiStatus)));
    }
}
