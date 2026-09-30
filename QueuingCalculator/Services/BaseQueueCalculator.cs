using QueuingCalculator.Models;

namespace QueuingCalculator.Services;

// Shared logic every model uses once Lq is known: only Lq differs between M/M/1, M/G/1 and G/G/1.
public abstract class BaseQueueCalculator : IQueueCalculator
{
    public QueueResult Calculate(double lambda, double mu,
        DistributionParams arrivalParams, DistributionParams serviceParams)
    {
        var result = new QueueResult();

        if (lambda <= 0 || mu <= 0)
        {
            result.ErrorMessage = "λ and μ must be positive.";
            return result;
        }

        double rho = lambda / mu;
        if (rho >= 1.0)
        {
            result.IsStable = false;
            result.Rho = rho;
            result.Rho0 = 1 - rho;
            result.ErrorMessage = "System is unstable (ρ ≥ 1): queue grows unboundedly. Reduce λ or increase μ.";
            return result;
        }

        double lq = ComputeLq(lambda, mu, rho, arrivalParams, serviceParams);
        return FinishFromLq(lambda, mu, rho, lq);
    }

    protected abstract double ComputeLq(double lambda, double mu, double rho,
        DistributionParams arrivalParams, DistributionParams serviceParams);

    private static QueueResult FinishFromLq(double lambda, double mu, double rho, double lq)
    {
        double wq = lq / lambda;
        double w = wq + 1.0 / mu;
        double l = lambda * w;
        return new QueueResult
        {
            Rho = rho,
            Rho0 = 1 - rho,
            Lq = lq,
            L = l,
            Wq = wq,
            W = w,
            IsStable = true
        };
    }
}
