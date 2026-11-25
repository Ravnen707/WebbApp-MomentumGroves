using MomentumGroves.Data;
using MomentumGroves.Models;

namespace MomentumGroves.Service;

/// <summary>
/// Reports the active storage providers and mobile data configuration for the Admin page.
/// </summary>
/// <param name="database">The configured personal database.</param>
/// <param name="images">The service that manages exercise pictures.</param>
public sealed class AdminTechnicalStatusService(PersonalDatabase database, TrainingImageService images)
{
    /// <summary>
    /// Summarizes the providers selected at startup without testing connections.
    /// </summary>
    /// <returns>Display labels for the active providers and mobile data API setting.</returns>
    public AdminTechnicalStatus GetStatus()
    {
        return new AdminTechnicalStatus(
            database.UsesPostgres ? "Supabase" : "Standalone",
            database.UsesPostgres ? "Supabase PostgreSQL" : "SQLite",
            images.UsesCloud ? "Supabase Storage (private bucket)" : "Local folder (wwwroot/TrainingImages)",
            "Local SQLite (frida.db)",
            database.MobileDataApiEnabled ? "Enabled" : "Disabled");
    }
}