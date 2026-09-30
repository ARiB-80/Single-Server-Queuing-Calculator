namespace QueuingCalculator.Models;

public class QueueResult
{
    public double Rho { get; set; }     // ρ — utilization / active proportion
    public double Rho0 { get; set; }    // ρ₀ — idle proportion
    public double L { get; set; }
    public double Lq { get; set; }
    public double W { get; set; }
    public double Wq { get; set; }
    public bool IsStable { get; set; }
    public string? ErrorMessage { get; set; }
}
