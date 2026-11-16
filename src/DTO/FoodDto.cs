namespace MomentumGroves.DTO;

/// <summary>
/// Represents a locally stored food with nutrition per 100 g or 100 ml.
/// </summary>
public sealed class FoodDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = "g";
    public double Kcal { get; set; }
    public double Protein { get; set; }
    public double Carbs { get; set; }
    public double Fat { get; set; }
    public double? Fiber { get; set; }
}
