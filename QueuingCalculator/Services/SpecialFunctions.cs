namespace QueuingCalculator.Services;

public static class SpecialFunctions
{
    // Lanczos approximation (g = 7, n = 9), accurate to ~15 significant digits.
    private static readonly double[] LanczosCoefficients =
    {
        0.99999999999980993, 676.5203681218851, -1259.1392167224028,
        771.32342877765313, -176.61502916214059, 12.507343278686905,
        -0.13857109526572012, 9.9843695780195716e-6, 1.5056327351493116e-7
    };

    // ln Γ(x) for x > 0.
    public static double LogGamma(double x)
    {
        if (x <= 0 || double.IsNaN(x)) return double.NaN;
        if (x < 0.5) // reflection: Γ(x)Γ(1−x) = π / sin(πx)
            return Math.Log(Math.PI / Math.Sin(Math.PI * x)) - LogGamma(1 - x);

        x -= 1;
        double a = LanczosCoefficients[0];
        double t = x + 7.5;
        for (int i = 1; i < LanczosCoefficients.Length; i++)
            a += LanczosCoefficients[i] / (x + i);
        return 0.5 * Math.Log(2 * Math.PI) + (x + 0.5) * Math.Log(t) - t + Math.Log(a);
    }

    // Γ(x) for x > 0.
    public static double Gamma(double x) => Math.Exp(LogGamma(x));
}
