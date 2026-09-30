using QueuingCalculator.Models;

namespace QueuingCalculator.Services;

public class GG1Calculator : BaseQueueCalculator
{
    protected override double ComputeLq(double lambda, double mu, double rho,
        DistributionParams arrivalParams, DistributionParams serviceParams)
    {
        // Lq = ρ²(1 + Cs²)(Ca² + ρ²Cs²) / [2(1 − ρ)(1 + ρ²Cs²)]
        double ca2 = arrivalParams.SCV;
        double cs2 = serviceParams.SCV;
        double numerator = rho * rho * (1 + cs2) * (ca2 + rho * rho * cs2);
        double denominator = 2 * (1 - rho) * (1 + rho * rho * cs2);
        return numerator / denominator;
    }
}
