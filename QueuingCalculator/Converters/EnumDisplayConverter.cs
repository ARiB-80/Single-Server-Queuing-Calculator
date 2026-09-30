using System.Globalization;
using Avalonia.Data.Converters;
using QueuingCalculator.Models;

namespace QueuingCalculator.Converters;

// Friendly names for enum values shown in ComboBoxes.
public class EnumDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        QueueModelType.MM1 => "M/M/1",
        QueueModelType.MG1 => "M/G/1",
        QueueModelType.GG1 => "G/G/1",
        DistributionType.PoissonExponential => "Poisson / Exponential",
        DistributionType.Uniform => "Uniform",
        DistributionType.Gamma => "Gamma",
        DistributionType.Normal => "Normal",
        DistributionType.Weibull => "Weibull",
        _ => value?.ToString()
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
