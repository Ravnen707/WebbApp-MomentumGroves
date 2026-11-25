namespace MomentumGroves.Models;

public sealed record TrainingWeightEdit(Guid ChangeId, string ExerciseId, decimal BaseKg, decimal Kg);
