namespace MomentumGroves.Models;

public record WorkoutExercise(
    string Name,
    int Sets,
    int Reps,
    decimal Kg);