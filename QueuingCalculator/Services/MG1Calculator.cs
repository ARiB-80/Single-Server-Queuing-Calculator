using QueuingCalculator.Models;

namespace QueuingCalculator.Services;

// Pollaczek–Khinchine
public class MG1Calculator : BaseQueueCalculator
{
    protected override double ComputeLq(double lambda, double mu, double rho,
        DistributionParams arrivalParams, DistributionParams serviceParams)
    {
        // Lq = (λ² σs² + ρ²) / [2(1 − ρ)]
        double sigmaS2 = serviceParams.Variance;
        return (lambda * lambda * sigmaS2 + rho * rho) / (2 * (1 - rho));
    }
}
