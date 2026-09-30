using QueuingCalculator.Services;

namespace QueuingCalculator.Models;

public class DistributionParams
{
    public DistributionType Type { get; set; }
    public double Rate { get; set; }          // λ or μ; mean = 1/Rate for every type except Uniform
    public double UniformA { get; set; }      // used when Type == Uniform
    public double UniformB { get; set; }      // used when Type == Uniform
    public double GammaShapeK { get; set; }   // used when Type == Gamma (scale derived from Rate so mean = 1/Rate)
    public double NormalSigma { get; set; }   // used when Type == Normal (standard deviation)
    public double WeibullShapeK { get; set; } // used when Type == Weibull (scale derived from Rate so mean = 1/Rate)

    // Set when the user enters σ², σ, C² or C directly; replaces the distribution's own variance.
    public double? VarianceOverride { get; set; }

    public double Mean => Type switch
    {
        DistributionType.PoissonExponential => 1.0 / Rate,
        DistributionType.Uniform => (UniformA + UniformB) / 2.0,
        DistributionType.Gamma => 1.0 / Rate, // scale θ = 1/(Rate·k) is chosen so k·θ = 1/Rate
        DistributionType.Normal => 1.0 / Rate,
        DistributionType.Weibull => 1.0 / Rate, // scale = (1/Rate) / Γ(1 + 1/k)
        _ => throw new ArgumentOutOfRangeException()
    };

    public double Variance => VarianceOverride ?? Type switch
    {
        DistributionType.PoissonExponential => 1.0 / (Rate * Rate),
        DistributionType.Uniform => Math.Pow(UniformB - UniformA, 2) / 12.0,
        DistributionType.Gamma => GammaShapeK * Math.Pow(1.0 / (Rate * GammaShapeK), 2),
        DistributionType.Normal => NormalSigma * NormalSigma,
        // Var = scale²[Γ(1+2/k) − Γ(1+1/k)²] = Mean²[Γ(1+2/k)/Γ(1+1/k)² − 1]; ratio taken in log space to avoid overflow
        DistributionType.Weibull => Mean * Mean * (Math.Exp(
            SpecialFunctions.LogGamma(1 + 2 / WeibullShapeK) - 2 * SpecialFunctions.LogGamma(1 + 1 / WeibullShapeK)) - 1),
        _ => throw new ArgumentOutOfRangeException()
    };

    public double SCV => Variance / (Mean * Mean); // squared coefficient of variation, used by G/G/1
}
