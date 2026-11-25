using System.Text.Json;

namespace MomentumGroves.Models;

public sealed record TrainingSave(JsonElement Programmes, Dictionary<string, decimal> OriginalWeights);
