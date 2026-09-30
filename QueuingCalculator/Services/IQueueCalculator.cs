using QueuingCalculator.Models;

namespace QueuingCalculator.Services;

public interface IQueueCalculator
{
    QueueResult Calculate(double lambda, double mu,
        DistributionParams arrivalParams, DistributionParams serviceParams);
}
