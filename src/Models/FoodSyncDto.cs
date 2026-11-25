namespace MomentumGroves.Models;

/// <summary>A mobile food sent to the desktop library.</summary>
public sealed class FoodSyncDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = "g";
    public double Kcal { get; set; }
    public double Protein { get; set; }
    public double Carbs { get; set; }
    public double Fat { get; set; }
    public double? Fiber { get; set; }
}
