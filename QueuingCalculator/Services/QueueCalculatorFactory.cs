using QueuingCalculator.Models;

namespace QueuingCalculator.Services;

public static class QueueCalculatorFactory
{
    public static IQueueCalculator Create(QueueModelType type) => type switch
    {
        QueueModelType.MM1 => new MM1Calculator(),
        QueueModelType.MG1 => new MG1Calculator(),
        QueueModelType.GG1 => new GG1Calculator(),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
