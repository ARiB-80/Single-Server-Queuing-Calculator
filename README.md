# Single-Server Queuing Calculator

A cross-platform desktop calculator for single-server queues: **M/M/1**, **M/G/1** and **G/G/1**.
Built with C# / .NET 8 and [Avalonia UI](https://avaloniaui.net/) using a hand-rolled MVVM pattern — no NuGet packages beyond Avalonia itself.

Runs on Linux, Windows and macOS.

## Features

- **Three queue models**
  - **M/M/1** — exponential arrivals and service
  - **M/G/1** — exponential arrivals, general service (Pollaczek–Khinchine)
  - **G/G/1** — general arrivals and service (approximation using Cₐ² and Cₛ²)
- **Five distributions** per side: Poisson / Exponential, Uniform, Gamma, Normal, Weibull
- **Enter what you know** — every derived statistic is also an input:
  - **Mean** (1/λ or 1/μ): typing a mean sets the rate, and vice versa
  - **σ², σ, C², C**: typing any one overrides the distribution and recomputes the other three; **Reset** returns to the distribution's values
- **Time units**: seconds, minutes or hours (rates are entered per unit; times are reported in the same unit)
- **Validation as you type**: Calculate is disabled until inputs are valid, and unstable systems (ρ ≥ 1) are reported instead of producing results
- **Results**: ρ, ρ₀, Lq, Wq, W, L, plus the Cₐ², Cₛ² and σₛ² actually used by the formulas

## Formulas

Only **Lq** differs between models; the rest follow from it.

| Model | Lq |
|---|---|
| M/M/1 | ρ² / (1 − ρ) |
| M/G/1 | (λ²σₛ² + ρ²) / [2(1 − ρ)] |
| G/G/1 | ρ²(1 + Cₛ²)(Cₐ² + ρ²Cₛ²) / [2(1 − ρ)(1 + ρ²Cₛ²)] |

Then, for every model:

- ρ = λ / μ, ρ₀ = 1 − ρ
- Wq = Lq / λ
- W = Wq + 1/μ
- L = λW

### Distribution parameterisation

λ and μ always mean *1 / mean time*. Each distribution adds a shape or spread parameter on top:

| Distribution | Extra input | Mean | Variance |
|---|---|---|---|
| Poisson / Exponential | — | 1/rate | 1/rate² |
| Uniform | a, b | (a + b)/2 | (b − a)²/12 |
| Gamma | shape k | 1/rate | 1/(k · rate²) |
| Normal | σ | 1/rate | σ² |
| Weibull | shape k | 1/rate | mean² · [Γ(1+2/k)/Γ(1+1/k)² − 1] |

C² (squared coefficient of variation) = variance / mean².

> **Uniform note:** its mean comes from (a + b)/2, while ρ uses λ/μ. Choose a and b whose midpoint matches 1/rate for consistent results.

## Getting started

### Prerequisites

[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). On Arch/CachyOS:

```sh
sudo pacman -S dotnet-sdk-8.0
```

### Build and run

```sh
dotnet build                                   # builds the app and the tests
dotnet run --project QueuingCalculator         # launches the calculator
dotnet run --project QueuingCalculator.Tests   # runs the test suite
```

## Tests

`QueuingCalculator.Tests` is a dependency-free console runner that compiles the Models, Services and ViewModels directly. It checks, among other things:

- M/M/1 with λ = 2, μ = 5 → ρ = 0.4, Lq = 0.2667, Wq = 0.1333, W = 0.3333, L = 0.6667
- M/G/1 and G/G/1 reduce exactly to M/M/1 when all distributions are exponential
- λ = 6, μ = 5 (and ρ = 1 exactly) are flagged as unstable
- M/G/1 with σₛ² = 0 matches the M/D/1 result
- Gamma and Weibull with k = 1 behave as exponential; Weibull k = 2 gives C² = 4/π − 1
- ViewModel behaviour: typed statistics override the distribution, the mean ↔ rate link, Reset, and model-driven locking

It exits with code 0 when all checks pass.

## Project structure

```
QueuingCalculator/
├── Models/        enums, DistributionParams (mean/variance/SCV), QueueResult
├── Services/      M/M/1, M/G/1, G/G/1 calculators, factory, validation, Gamma function
├── ViewModels/    MainViewModel, per-side DistributionInputViewModel, MVVM plumbing
├── Views/         MainWindow and the reusable DistributionEditor control
└── Converters/    enum display names, 4-decimal number formatting
QueuingCalculator.Tests/   console test runner
```

The `Models/` and `Services/` layers have no UI dependencies.
