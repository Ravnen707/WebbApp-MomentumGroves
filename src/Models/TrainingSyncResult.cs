using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace MomentumGroves.Models;

public sealed record TrainingSyncResult(
    JsonObject Programmes,
    List<TrainingWeightResult> Results)
{
    /// <summary>
    /// Computes a version fingerprint from the serialized training programmes.
    /// </summary>
    /// <returns>The SHA-256 fingerprint as a hexadecimal string.</returns>
    public string Version()
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Programmes.ToJsonString())));
    }
}
