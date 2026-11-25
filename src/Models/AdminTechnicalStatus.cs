namespace MomentumGroves.Models;

public sealed record AdminTechnicalStatus(
    string Mode,
    string Database,
    string ExerciseImageStorage,
    string FridaStorage,
    string MobileDataApi);
