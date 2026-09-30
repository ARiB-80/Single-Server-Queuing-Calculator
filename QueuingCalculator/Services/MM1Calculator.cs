using QueuingCalculator.Models;

namespace QueuingCalculator.Services;

public class MM1Calculator : BaseQueueCalculator
{
    protected override double ComputeLq(double lambda, double mu, double rho,
        DistributionParams arrivalParams, DistributionParams serviceParams)
    {
        // Lq = ρ² / (1 − ρ)
        return (rho * rho) / (1 - rho);
    }
}
