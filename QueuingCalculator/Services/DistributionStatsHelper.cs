using QueuingCalculator.Models;

namespace QueuingCalculator.Services;

// Builds and validates DistributionParams so the ViewModel and tests share one set of rules.
public static class DistributionStatsHelper
{
    public static DistributionParams Build(DistributionType type, double rate,
        double uniformA = 0, double uniformB = 0, double gammaK = 0,
        double normalSigma = 0, double weibullK = 0) => new()
    {
        Type = type,
        Rate = rate,
        UniformA = uniformA,
        UniformB = uniformB,
        GammaShapeK = gammaK,
        NormalSigma = normalSigma,
        WeibullShapeK = weibullK
    };

    public static DistributionParams Exponential(double rate) => Build(DistributionType.PoissonExponential, rate);

    // Converts a directly entered σ², σ, C² or C into a variance. Negative or non-numeric input gives NaN.
    public static double VarianceFrom(StatKind kind, double value, double mean)
    {
        if (!double.IsFinite(value) || value < 0) return double.NaN;
        return kind switch
        {
            StatKind.Variance => value,
            StatKind.StdDev => value * value,
            StatKind.Scv => value * mean * mean,
            StatKind.Cv => value * value * mean * mean,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    // Returns null when valid, otherwise a message naming the offending field. `side` is e.g. "Service".
    public static string? Validate(DistributionParams p, string side)
    {
        // An override replaces the distribution entirely, so its parameters are not checked.
        if (p.VarianceOverride is { } v)
            return double.IsFinite(v) && v >= 0 ? null : $"{side}: entered σ², σ, C² or C must be a number ≥ 0.";

        string? error = p.Type switch
        {
            DistributionType.Uniform when !double.IsFinite(p.UniformA) || !double.IsFinite(p.UniformB)
                => $"{side} Uniform: a and b must be numbers.",
            DistributionType.Uniform when p.UniformA < 0
                => $"{side} Uniform: a must be ≥ 0 (times cannot be negative).",
            DistributionType.Uniform when p.UniformA >= p.UniformB
                => $"{side} Uniform: a must be less than b.",
            DistributionType.Gamma when !(p.GammaShapeK > 0) || !double.IsFinite(p.GammaShapeK)
                => $"{side} Gamma: shape k must be > 0.",
            DistributionType.Normal when !(p.NormalSigma >= 0) || !double.IsFinite(p.NormalSigma)
                => $"{side} Normal: σ must be a number ≥ 0.",
            DistributionType.Weibull when !(p.WeibullShapeK > 0) || !double.IsFinite(p.WeibullShapeK)
                => $"{side} Weibull: shape k must be > 0.",
            _ => null
        };

        if (error is null && !double.IsFinite(p.Variance))
            error = $"{side}: variance is not finite — the shape parameter is too extreme.";
        return error;
    }
}
