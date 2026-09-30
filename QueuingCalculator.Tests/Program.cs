using QueuingCalculator.Models;
using QueuingCalculator.Services;
using QueuingCalculator.ViewModels;

int failures = 0;

void Check(string name, double actual, double expected, double tol = 1e-4)
{
    bool ok = Math.Abs(actual - expected) <= tol;
    if (!ok) failures++;
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name,-45} actual={actual:0.######}  expected={expected:0.######}");
}

void Assert(string name, bool condition)
{
    if (!condition) failures++;
    Console.WriteLine($"{(condition ? "PASS" : "FAIL")}  {name}");
}

QueueResult Run(QueueModelType model, double lambda, double mu, DistributionParams arrival, DistributionParams service)
    => QueueCalculatorFactory.Create(model).Calculate(lambda, mu, arrival, service);

void CheckMatchesMM1(string label, QueueResult r)
{
    Assert($"{label}: stable, no error", r.IsStable && r.ErrorMessage is null);
    Check($"{label}: ρ", r.Rho, 0.4);
    Check($"{label}: ρ₀", r.Rho0, 0.6);
    Check($"{label}: Lq", r.Lq, 0.4 * 0.4 / 0.6);
    Check($"{label}: Wq", r.Wq, 0.4 * 0.4 / 0.6 / 2);
    Check($"{label}: W", r.W, 1.0 / 3);
    Check($"{label}: L", r.L, 2.0 / 3);
}

var expA = DistributionStatsHelper.Exponential(2);
var expS = DistributionStatsHelper.Exponential(5);

Console.WriteLine("== M/M/1, λ=2, μ=5 ==");
var mm1 = Run(QueueModelType.MM1, 2, 5, expA, expS);
CheckMatchesMM1("M/M/1", mm1);

Console.WriteLine("\n== M/G/1 with exponential service reduces to M/M/1 ==");
var mg1 = Run(QueueModelType.MG1, 2, 5, expA, expS);
CheckMatchesMM1("M/G/1(exp)", mg1);
Check("M/G/1(exp) Lq == M/M/1 Lq", mg1.Lq, mm1.Lq, 1e-12);

Console.WriteLine("\n== G/G/1 with exponential both sides reduces to M/M/1 ==");
var gg1 = Run(QueueModelType.GG1, 2, 5, expA, expS);
CheckMatchesMM1("G/G/1(exp,exp)", gg1);
Check("G/G/1(exp,exp) Lq == M/M/1 Lq", gg1.Lq, mm1.Lq, 1e-12);

Console.WriteLine("\n== Gamma with k=1 is exponential ==");
var gammaK1 = DistributionStatsHelper.Build(DistributionType.Gamma, 5, gammaK: 1);
Check("Gamma(k=1) variance == 1/μ²", gammaK1.Variance, 1.0 / 25, 1e-12);
Check("Gamma(k=1) SCV", gammaK1.SCV, 1.0, 1e-12);
var gammaK4 = DistributionStatsHelper.Build(DistributionType.Gamma, 5, gammaK: 4);
Check("Gamma(k=4) mean == 1/μ", gammaK4.Mean, 0.2, 1e-12);
Check("Gamma(k=4) SCV == 1/k", gammaK4.SCV, 0.25, 1e-12);

Console.WriteLine("\n== Instability: λ=6, μ=5 ==");
var unstable = Run(QueueModelType.MM1, 6, 5, DistributionStatsHelper.Exponential(6), expS);
Assert("unstable flagged", !unstable.IsStable && unstable.ErrorMessage is not null);
Console.WriteLine($"      message: {unstable.ErrorMessage}");
var boundary = Run(QueueModelType.MM1, 5, 5, DistributionStatsHelper.Exponential(5), expS);
Assert("ρ = 1 exactly is unstable", !boundary.IsStable && boundary.ErrorMessage is not null);
var nonPositive = Run(QueueModelType.MM1, 0, 5, expA, expS);
Assert("λ = 0 rejected", nonPositive.ErrorMessage is not null);

Console.WriteLine("\n== Uniform service spot check: U(0.1, 0.3), λ=2, μ=5 ==");
var uni = DistributionStatsHelper.Build(DistributionType.Uniform, 5, 0.1, 0.3);
Check("Uniform variance = 0.2²/12", uni.Variance, 0.04 / 12, 1e-12);
Check("Uniform mean", uni.Mean, 0.2, 1e-12);
var mg1Uni = Run(QueueModelType.MG1, 2, 5, expA, uni);
// P-K: (4·0.003333 + 0.16) / (2·0.6) = 0.144444
Check("M/G/1 uniform Lq", mg1Uni.Lq, (4 * 0.04 / 12 + 0.16) / 1.2);
Assert("Lq changed vs exponential service", Math.Abs(mg1Uni.Lq - mg1.Lq) > 1e-6);
Assert("Lower service variance => lower Lq", mg1Uni.Lq < mg1.Lq);

Console.WriteLine("\n== Validation helper ==");
Assert("a ≥ b rejected", DistributionStatsHelper.Validate(DistributionStatsHelper.Build(DistributionType.Uniform, 5, 0.3, 0.1), "Service") is not null);
Assert("k = 0 rejected", DistributionStatsHelper.Validate(DistributionStatsHelper.Build(DistributionType.Gamma, 5, gammaK: 0), "Service") is not null);
Assert("valid uniform accepted", DistributionStatsHelper.Validate(uni, "Service") is null);

Console.WriteLine("\n== Gamma function ==");
Check("Γ(0.5) = √π", SpecialFunctions.Gamma(0.5), Math.Sqrt(Math.PI), 1e-12);
Check("Γ(5) = 24", SpecialFunctions.Gamma(5), 24, 1e-10);
Check("Γ(1.5) = √π/2", SpecialFunctions.Gamma(1.5), Math.Sqrt(Math.PI) / 2, 1e-12);

Console.WriteLine("\n== Weibull ==");
var weibullK1 = DistributionStatsHelper.Build(DistributionType.Weibull, 5, weibullK: 1);
Check("Weibull(k=1) mean == 1/μ", weibullK1.Mean, 0.2, 1e-12);
Check("Weibull(k=1) SCV == 1 (exponential)", weibullK1.SCV, 1, 1e-10);
var weibullK2 = DistributionStatsHelper.Build(DistributionType.Weibull, 5, weibullK: 2);
Check("Weibull(k=2) SCV == 4/π − 1", weibullK2.SCV, 4 / Math.PI - 1, 1e-10);
var gg1Weibull = Run(QueueModelType.GG1, 2, 5, DistributionStatsHelper.Build(DistributionType.Weibull, 2, weibullK: 1), weibullK1);
Check("G/G/1 Weibull(k=1) both sides Lq == M/M/1 Lq", gg1Weibull.Lq, mm1.Lq, 1e-9);

Console.WriteLine("\n== Normal ==");
var normal = DistributionStatsHelper.Build(DistributionType.Normal, 5, normalSigma: 0.05);
Check("Normal mean == 1/μ", normal.Mean, 0.2, 1e-12);
Check("Normal variance == σ²", normal.Variance, 0.0025, 1e-12);
Check("Normal SCV", normal.SCV, 0.0625, 1e-12);

Console.WriteLine("\n== Directly entered statistics (variance override) ==");
Check("VarianceFrom(C² = 1, mean 0.2)", DistributionStatsHelper.VarianceFrom(StatKind.Scv, 1, 0.2), 0.04, 1e-12);
Check("VarianceFrom(C = 0.5, mean 0.2)", DistributionStatsHelper.VarianceFrom(StatKind.Cv, 0.5, 0.2), 0.01, 1e-12);
Check("VarianceFrom(σ = 0.1)", DistributionStatsHelper.VarianceFrom(StatKind.StdDev, 0.1, 0.2), 0.01, 1e-12);
Assert("VarianceFrom(negative) is NaN", double.IsNaN(DistributionStatsHelper.VarianceFrom(StatKind.Variance, -1, 0.2)));

var deterministic = DistributionStatsHelper.Build(DistributionType.Gamma, 5, gammaK: 2);
deterministic.VarianceOverride = 0;
var md1 = Run(QueueModelType.MG1, 2, 5, expA, deterministic);
Check("M/G/1 with σs² = 0 (M/D/1) Lq = ρ²/(2(1−ρ))", md1.Lq, 0.16 / 1.2);

var ca = DistributionStatsHelper.Build(DistributionType.Uniform, 2, 0.4, 0.6);
ca.VarianceOverride = DistributionStatsHelper.VarianceFrom(StatKind.Scv, 1, ca.Mean);
var cs = DistributionStatsHelper.Build(DistributionType.Normal, 5, normalSigma: 0.01);
cs.VarianceOverride = DistributionStatsHelper.VarianceFrom(StatKind.Scv, 1, cs.Mean);
Check("Override Ca² = 1 reaches SCV", ca.SCV, 1, 1e-12);
Check("G/G/1 with Ca² = Cs² = 1 overrides Lq == M/M/1 Lq", Run(QueueModelType.GG1, 2, 5, ca, cs).Lq, mm1.Lq, 1e-12);

Console.WriteLine("\n== New validation rules ==");
Assert("Weibull k = 0 rejected", DistributionStatsHelper.Validate(DistributionStatsHelper.Build(DistributionType.Weibull, 5, weibullK: 0), "Service") is not null);
Assert("Weibull k = 0.001 (overflow) rejected", DistributionStatsHelper.Validate(DistributionStatsHelper.Build(DistributionType.Weibull, 5, weibullK: 0.001), "Service") is not null);
Assert("Normal σ < 0 rejected", DistributionStatsHelper.Validate(DistributionStatsHelper.Build(DistributionType.Normal, 5, normalSigma: -1), "Service") is not null);
var badOverride = DistributionStatsHelper.Exponential(5);
badOverride.VarianceOverride = double.NaN;
Assert("invalid override rejected", DistributionStatsHelper.Validate(badOverride, "Service") is not null);
var overrideHidesBadParams = DistributionStatsHelper.Build(DistributionType.Uniform, 5, 0.3, 0.1);
overrideHidesBadParams.VarianceOverride = 0.01;
Assert("override ignores invalid distribution params", DistributionStatsHelper.Validate(overrideHidesBadParams, "Service") is null);

Console.WriteLine("\n== ViewModel: editable statistics ==");
var vm = new MainViewModel { SelectedModel = QueueModelType.GG1 };
Assert("G/G/1: both sides editable", vm.Arrival.IsEditable && vm.Service.IsEditable);
Assert("exponential service shows Cₛ² = 1", vm.Service.ScvText == "1");
Assert("exponential service shows σₛ² = 0.04", vm.Service.VarianceText == "0.04");

vm.Service.SelectedType = DistributionType.Gamma; // k = 2 → C² = 0.5
Assert("Gamma(k=2) shows Cₛ² = 0.5", vm.Service.ScvText == "0.5");

vm.Service.ScvText = "0.25"; // user types a known Cs²
Assert("typing Cₛ² sets override", vm.Service.IsOverridden && !vm.Service.ParamsEditable);
Assert("typed box is kept as typed", vm.Service.ScvText == "0.25");
Assert("σₛ² recomputed = C²·mean² = 0.01", vm.Service.VarianceText == "0.01");
Assert("σₛ recomputed = 0.1", vm.Service.StdDevText == "0.1");
Assert("Cₛ recomputed = 0.5", vm.Service.CvText == "0.5");

vm.MuText = "4"; // rate change keeps C² fixed, variance follows (mean 0.25)
Assert("μ change keeps typed Cₛ²", vm.Service.ScvText == "0.25");
Assert("μ change recomputes σₛ² = 0.25·0.25² = 0.015625", vm.Service.VarianceText == "0.015625");
vm.MuText = "5";

vm.Arrival.ScvText = "1";
vm.CalculateCommand.Execute(null);
double expectedLq = 0.16 * 1.25 * (1 + 0.16 * 0.25) / (2 * 0.6 * (1 + 0.16 * 0.25));
Check("G/G/1 Lq with Ca² = 1, Cs² = 0.25", vm.Lq ?? double.NaN, expectedLq, 1e-9);
Check("Cₛ² used shown in results", vm.CsSquared ?? double.NaN, 0.25, 1e-12);

vm.Service.VarianceText = "-1";
Assert("negative override blocks Calculate", !vm.CalculateCommand.CanExecute(null) && vm.HasValidationMessage);
vm.Service.VarianceText = "0";
Assert("σ² = 0 is accepted (deterministic)", vm.CalculateCommand.CanExecute(null));

vm.Service.ResetCommand.Execute(null);
Assert("Reset clears override", !vm.Service.IsOverridden && vm.Service.ScvText == "0.5");

vm.Service.ScvText = "0.3";
vm.Service.SelectedType = DistributionType.Weibull;
Assert("changing distribution type clears override", !vm.Service.IsOverridden);
Check("Weibull(k=2) Cₛ² shown", double.Parse(vm.Service.ScvText, System.Globalization.CultureInfo.InvariantCulture), 4 / Math.PI - 1, 1e-5);

vm.Arrival.ScvText = "2";
vm.SelectedModel = QueueModelType.MG1;
Assert("M/G/1 locks arrival and drops its override", !vm.Arrival.IsEditable && !vm.Arrival.IsOverridden && vm.Arrival.ScvText == "1");
vm.SelectedModel = QueueModelType.MM1;
Assert("M/M/1 locks service back to exponential", !vm.Service.IsEditable && vm.Service.SelectedType == DistributionType.PoissonExponential);

Console.WriteLine("\n== ViewModel: editable mean ==");
var vm2 = new MainViewModel();
Assert("mean shows 1/λ = 0.5", vm2.Arrival.MeanText == "0.5");
Assert("mean shows 1/μ = 0.2", vm2.Service.MeanText == "0.2");

vm2.Arrival.MeanText = "0.25"; // works even while M/M/1 locks the distribution
Assert("typing arrival mean sets λ = 4", vm2.LambdaText == "4");
Assert("typed mean box is kept as typed", vm2.Arrival.MeanText == "0.25");
Assert("arrival σ² follows new mean (0.0625)", vm2.Arrival.VarianceText == "0.0625");

vm2.MuText = "8";
Assert("typing μ updates the service mean box", vm2.Service.MeanText == "0.125");

vm2.Service.MeanText = "";
Assert("blank mean blanks μ and blocks Calculate", vm2.MuText == "" && !vm2.CalculateCommand.CanExecute(null));
vm2.Service.MeanText = "0.2";
Assert("valid mean restores μ = 5", vm2.MuText == "5" && vm2.CalculateCommand.CanExecute(null));

vm2.SelectedModel = QueueModelType.GG1;
vm2.Service.ScvText = "0.5";
vm2.Service.MeanText = "0.1";
Assert("mean change keeps typed Cₛ², σₛ² follows (0.005)", vm2.Service.ScvText == "0.5" && vm2.Service.VarianceText == "0.005" && vm2.MuText == "10");

vm2.Service.SelectedType = DistributionType.Uniform;
Assert("Uniform mean is read-only and shows (a+b)/2", !vm2.Service.IsMeanEditable && vm2.Service.MeanText == "0.2");
vm2.Service.MeanText = "1";
Assert("editing Uniform mean does not change μ", vm2.MuText == "10");

Console.WriteLine(failures == 0 ? "\nAll checks passed." : $"\n{failures} check(s) FAILED.");
return failures == 0 ? 0 : 1;
