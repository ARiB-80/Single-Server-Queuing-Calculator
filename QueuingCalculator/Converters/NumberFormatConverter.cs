using System.Globalization;
using Avalonia.Data.Converters;

namespace QueuingCalculator.Converters;

// Formats result values to 4 decimal places; shows a dash when there is no result.
public class NumberFormatConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        double d => d.ToString("0.0000", CultureInfo.InvariantCulture),
        _ => "—"
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
