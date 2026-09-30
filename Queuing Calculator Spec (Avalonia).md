# Single-Server Queuing Calculator — Implementation Spec (C# / Avalonia)

## Handoff Notes for Claude Code

This is a build spec, not a discussion doc. Follow it top to bottom. Where a decision isn't specified, prefer the simplest working option and keep moving — flag assumptions in a short summary at the end rather than stopping to ask.

**Target stack:** C# / .NET 8 / **Avalonia UI**, MVVM pattern, no external NuGet packages beyond the Avalonia templates themselves (pure math + hand-rolled MVVM plumbing, no charting or stats library, no ReactiveUI/CommunityToolkit dependency).

**Platform note:** this project targets Linux (developed on CachyOS) as well as Windows/macOS. WPF was the original candidate but is Windows-only; Avalonia is a cross-platform, open-source XAML UI framework with the same MVVM/data-binding model, so it's used here instead. The `Models/` and `Services/` layers below are pure C# and identical regardless of UI framework.

---

## 1. Project Setup

Use the plain Avalonia template (not the `avalonia.mvvm` template) so the project stays dependency-free — the `avalonia.mvvm` template pulls in CommunityToolkit.Mvvm, which isn't needed for an app this size.

```
dotnet new install Avalonia.Templates
dotnet new avalonia -n QueuingCalculator
```

Folder structure to create:

```
QueuingCalculator/
├── Models/
│   ├── QueueModelType.cs
│   ├── DistributionType.cs
│   ├── TimeUnit.cs
│   ├── DistributionParams.cs
│   └── QueueResult.cs
├── Services/
│   ├── IQueueCalculator.cs
│   ├── BaseQueueCalculator.cs
│   ├── MM1Calculator.cs
│   ├── MG1Calculator.cs
│   ├── GG1Calculator.cs
│   ├── DistributionStatsHelper.cs
│   └── QueueCalculatorFactory.cs
├── ViewModels/
│   ├── MainViewModel.cs
│   ├── RelayCommand.cs
│   └── ViewModelBase.cs
├── Views/
│   └── MainWindow.axaml
└── App.axaml
```

> Note the extension: Avalonia uses **`.axaml`** instead of WPF's `.xaml` (avoids clashing with the MSBuild XAML tooling that's Windows-specific). Same underlying markup language and concepts — data binding, resources, styles — just a different file extension and a few namespace/control differences covered in Section 6.

---

## 2. Enums

**`Models/QueueModelType.cs`**

```csharp
public enum QueueModelType { MM1, MG1, GG1 }
```

**`Models/DistributionType.cs`**

```csharp
public enum DistributionType { PoissonExponential, Uniform, Gamma }
```

> Poisson and Exponential are merged into a single option (`PoissonExponential`) per prior decision — both map to the same mean/variance/SCV math (rate-based, exponential interarrival/service times). Display label in UI: **"Poisson / Exponential"**.

**`Models/TimeUnit.cs`**

```csharp
public enum TimeUnit { Seconds, Minutes, Hours }
```

> UI uses **RadioButtons**, not checkboxes, for time unit — it's a mutually exclusive choice (a result is reported in exactly one unit). Use radio buttons even though the original request said "checkboxes."

*(Nothing about these enums changes between WPF and Avalonia — this whole layer is platform-agnostic.)*

---

## 3. Model Classes

**`Models/DistributionParams.cs`**

```csharp
public class DistributionParams
{
    public DistributionType Type { get; set; }
    public double Rate { get; set; }       // used when Type == PoissonExponential (λ or μ)
    public double UniformA { get; set; }   // used when Type == Uniform
    public double UniformB { get; set; }   // used when Type == Uniform
    public double GammaShapeK { get; set; }// used when Type == Gamma (scale derived from Rate so mean = 1/Rate)

    public double Mean => Type switch
    {
        DistributionType.PoissonExponential => 1.0 / Rate,
        DistributionType.Uniform => (UniformA + UniformB) / 2.0,
        DistributionType.Gamma => 1.0 / Rate, // scale θ = 1/(Rate·k) is chosen so k·θ = 1/Rate
        _ => throw new ArgumentOutOfRangeException()
    };

    public double Variance => Type switch
    {
        DistributionType.PoissonExponential => 1.0 / (Rate * Rate),
        DistributionType.Uniform => Math.Pow(UniformB - UniformA, 2) / 12.0,
        DistributionType.Gamma => GammaShapeK * Math.Pow(1.0 / (Rate * GammaShapeK), 2),
        _ => throw new ArgumentOutOfRangeException()
    };

    public double SCV => Variance / (Mean * Mean); // squared coefficient of variation, used by G/G/1
}
```

> **Gamma parameterization decision:** the UI only collects a shape parameter `k` from the user (plus the rate λ or μ that's already entered globally). Derive the Gamma scale θ so that the distribution's mean equals `1/Rate` (i.e. θ = 1/(Rate·k)). This keeps the input model consistent — λ/μ always represents "1 / mean" regardless of which distribution is selected, and Gamma just adds a shape/variance knob on top.

**`Models/QueueResult.cs`**

```csharp
public class QueueResult
{
    public double Rho { get; set; }     // ρ — utilization / active proportion
    public double Rho0 { get; set; }    // ρ₀ — idle proportion
    public double L { get; set; }
    public double Lq { get; set; }
    public double W { get; set; }
    public double Wq { get; set; }
    public bool IsStable { get; set; }
    public string? ErrorMessage { get; set; }
}
```

*(Identical to the WPF version — no UI framework dependency in this layer.)*

---

## 4. Calculation Services

**`Services/IQueueCalculator.cs`**

```csharp
public interface IQueueCalculator
{
    QueueResult Calculate(double lambda, double mu,
        DistributionParams arrivalParams, DistributionParams serviceParams);
}
```

**`Services/BaseQueueCalculator.cs`** — shared logic every model uses once `Lq` is known.

```csharp
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

    private QueueResult FinishFromLq(double lambda, double mu, double rho, double lq)
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
```

**`Services/MM1Calculator.cs`**

```csharp
public class MM1Calculator : BaseQueueCalculator
{
    protected override double ComputeLq(double lambda, double mu, double rho,
        DistributionParams arrivalParams, DistributionParams serviceParams)
    {
        // Lq = ρ² / (1 − ρ)
        return (rho * rho) / (1 - rho);
    }
}
```

**`Services/MG1Calculator.cs`** (Pollaczek–Khinchine)

```csharp
public class MG1Calculator : BaseQueueCalculator
{
    protected override double ComputeLq(double lambda, double mu, double rho,
        DistributionParams arrivalParams, DistributionParams serviceParams)
    {
        // Lq = (λ² σs² + ρ²) / [2(1 − ρ)]
        double sigmaS2 = serviceParams.Variance;
        return (lambda * lambda * sigmaS2 + rho * rho) / (2 * (1 - rho));
    }
}
```

**`Services/GG1Calculator.cs`** (from handwritten notes)

```csharp
public class GG1Calculator : BaseQueueCalculator
{
    protected override double ComputeLq(double lambda, double mu, double rho,
        DistributionParams arrivalParams, DistributionParams serviceParams)
    {
        // Lq = ρ²(1 + Cs²)(Ca² + ρ²Cs²) / [2(1 − ρ)(1 + ρ²Cs²)]
        double ca2 = arrivalParams.SCV;
        double cs2 = serviceParams.SCV;
        double numerator = rho * rho * (1 + cs2) * (ca2 + rho * rho * cs2);
        double denominator = 2 * (1 - rho) * (1 + rho * rho * cs2);
        return numerator / denominator;
    }
}
```

**`Services/QueueCalculatorFactory.cs`**

```csharp
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
```

*(This entire Services/ layer is pure C# with zero UI framework references — identical whether the UI is WPF, Avalonia, or a console app. No changes from the original plan.)*

---

## 5. ViewModel Behavior (`ViewModels/MainViewModel.cs`)

Avalonia's data-binding engine works the same way WPF's does: bind to public properties and raise `INotifyPropertyChanged`. Since we're avoiding external packages, hand-roll the plumbing exactly as you would in WPF:

**`ViewModels/ViewModelBase.cs`**

```csharp
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
```

**`ViewModels/RelayCommand.cs`** — implements `System.Windows.Input.ICommand`, which Avalonia's `Button.Command` binds against exactly like WPF does (same interface, part of the BCL, not framework-specific):

```csharp
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
```

Bindable properties needed on `MainViewModel`:

- `QueueModelType SelectedModel`
- `double Lambda`, `double Mu`
- `DistributionType SelectedArrivalDistribution`, `DistributionType SelectedServiceDistribution`
- `TimeUnit SelectedTimeUnit`
- Extra param fields: `ArrivalUniformA/B`, `ArrivalGammaK`, `ServiceUniformA/B`, `ServiceGammaK`
- Result fields: `Rho`, `Rho0`, `L`, `Lq`, `W`, `Wq` (formatted strings with unit suffix)
- `string? ErrorMessage`
- `ICommand CalculateCommand`

**Model-driven UI rules (implement via property-changed + visibility bindings, not code-behind):**

1. When `SelectedModel == MM1`: force both distributions to `PoissonExponential`, disable (grey out, don't hide) the two distribution selectors, since M/M/1 is exponential-only by definition.
2. When `SelectedModel == MG1`: arrival distribution selector is disabled and forced to `PoissonExponential`; service distribution selector is enabled (any of the three types).
3. When `SelectedModel == GG1`: both distribution selectors are enabled.
4. When a distribution selector is set to `Uniform`: show that side's `A`/`B` numeric inputs.
5. When a distribution selector is set to `Gamma`: show that side's shape `k` numeric input.
6. When set to `PoissonExponential`: hide extra param inputs — only λ (or μ) is used.

**Calculate command logic:**

1. Validate: λ > 0, μ > 0; if Uniform selected, A \< B and both finite; if Gamma selected, k > 0.
2. Build `DistributionParams` for arrival (rate = λ) and service (rate = μ) based on selected type and extra fields.
3. Get calculator via `QueueCalculatorFactory.Create(SelectedModel)`.
4. Call `Calculate(...)`.
5. If `result.ErrorMessage != null`, display it prominently (red text banner) and clear numeric outputs.
6. Otherwise populate Rho/Rho0/L/Lq/W/Wq fields, appending the unit label for the two time-valued outputs (W, Wq) based on `SelectedTimeUnit` (e.g., "2.35 min"). Rho/Rho0/L/Lq are unitless — do not append a time unit to those.

*(This whole section is unchanged from the WPF plan — MVVM as a pattern doesn't care which XAML-based framework renders the View.)*

---

## 6. Avalonia Layout Plan (`Views/MainWindow.axaml`)

This is the section that actually changes between WPF and Avalonia. Key differences to apply:

- **Root namespace**: use `xmlns="https://github.com/avaloniaui"` instead of WPF's `xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"`.
- **`x:Class`** still works the same way, pointing at `QueuingCalculator.Views.MainWindow`.
- **Most controls are named identically**: `Grid`, `StackPanel`, `TextBox`, `ComboBox`, `RadioButton`, `Button`, `GroupBox`... *except* Avalonia has no built-in `GroupBox`. Use a `Border` with a `HeaderedContentControl`, or simpler: a `Border` wrapping a `StackPanel` with a `TextBlock` header styled bold — either works fine for this app's needs, no need to overengineer it.
- **`TextBlock` vs `Label`**: Avalonia strongly prefers `TextBlock` for static text (same as WPF convention, but worth calling out since `Label` behaves slightly differently). Use `TextBlock` throughout for field labels and output display.
- **Bindings**: `{Binding Lambda}`, `{Binding SelectedModel}`, etc. — identical syntax to WPF. `IsEnabled="{Binding IsArrivalDistributionEditable}"` and `IsVisible="{Binding ShowUniformParams}"` work the same way, except Avalonia uses **`IsVisible`** (a bool) rather than WPF's `Visibility` (an enum requiring a `BooleanToVisibilityConverter`) — this is actually a small simplification, one less converter to write.
- **`DataContext`**: set the same way, either in XAML (`x:DataType`/`DataContext` in the `MainWindow` constructor) or in code-behind — set `DataContext = new MainViewModel();` in `MainWindow.axaml.cs`'s constructor.

Single window, two-column layout (structure unchanged from the WPF plan):

**Left column — Inputs**

- "Model" section: ComboBox bound to `QueueModelType`
- "Parameters" section:
  - TextBox for λ (with label "Arrival rate (λ)")
  - TextBox for μ (with label "Service rate (μ)")
- "Arrival Distribution" section:
  - ComboBox bound to `DistributionType` (IsEnabled bound per rules above)
  - Conditional panel: Uniform A/B fields, or Gamma k field (IsVisible-bound)
- "Service Distribution" section: mirrors arrival, same conditional logic
- "Time Unit" section: three RadioButtons (Seconds / Minutes / Hours), one bound group
- Button "Calculate" bound to `CalculateCommand`

**Right column — Outputs**

- Read-only display block or small grid, one row per metric:
  - ρ (Active/utilization)
  - ρ₀ (Idle)
  - L (Avg number in system)
  - Lq (Avg number in queue)
  - W (Avg time in system) — with unit
  - Wq (Avg time in queue) — with unit
- Error/warning banner area above the grid (IsVisible-bound to `ErrorMessage` being non-null)

Use `IValueConverter`s (Avalonia's `IValueConverter` interface, same shape as WPF's) for:

- Enum-to-friendly-string display in ComboBoxes (e.g., `PoissonExponential` → "Poisson / Exponential")
- Numeric formatting (round displayed results to 4 decimal places)
- No boolean-to-visibility converter needed — see `IsVisible` note above.

---

## 7. Validation Rules Checklist

- [ ] λ must be > 0 (reject blank, zero, negative, non-numeric)
- [ ] μ must be > 0
- [ ] ρ = λ/μ must be \< 1, else show instability error and block calculation
- [ ] If Uniform selected: A \< B, both finite numbers
- [ ] If Gamma selected: shape k > 0
- [ ] Disable the "Calculate" button (or show inline validation) while any required field is invalid, rather than only failing after click
- [ ] All numeric inputs should reject non-numeric characters at entry time — Avalonia's `TextBox` doesn't have a built-in numeric-only mode, so filter via the `TextInput` event (equivalent to WPF's `PreviewTextInput`) or validate in the property setter and mark the field invalid rather than throwing

---

## 8. Testing Checklist (manual or xUnit, either is fine)

Use these known values to sanity-check the math after implementation. This step is identical regardless of UI framework since it only touches Models/ and Services/ — worth running before wiring up any Avalonia views at all.

**M/M/1 sanity check:** λ = 2, μ = 5

- ρ = 0.4, ρ₀ = 0.6
- Lq = 0.4² / (1 − 0.4) = 0.2667
- Wq = Lq / λ = 0.1333
- W = Wq + 1/μ = 0.3333
- L = λW = 0.6667

**M/G/1 sanity check:** λ = 2, μ = 5, service = Exponential → should reduce to identical results as the M/M/1 case above (Exponential variance = 1/μ² makes the P-K formula collapse to the M/M/1 formula). Use this as a regression test — if M/G/1 with exponential service doesn't match M/M/1 exactly, there's a bug.

**G/G/1 sanity check:** λ = 2, μ = 5, both distributions Exponential (Ca² = Cs² = 1) → should also reduce to the same M/M1 numbers above. This is the other required regression test per the "reduces exactly to M/M/1" property noted in the formulas.

**Instability check:** λ = 6, μ = 5 → should show the instability error, not a result.

**Uniform/Gamma spot checks:** pick simple values (e.g. Uniform(a=0.1, b=0.3) for service time) and manually verify variance = (0.3−0.1)²/12 = 0.00333 is being picked up correctly by comparing Lq before/after changing distribution type with λ/μ held constant — Lq should change since σₛ² changed.

---

## 9. Build & Delivery Order (for Claude Code to follow sequentially)

1. Install the Avalonia templates and scaffold the project: `dotnet new install Avalonia.Templates` then `dotnet new avalonia -n QueuingCalculator`.
2. Implement all Models/ classes (enums, DistributionParams, QueueResult).
3. Implement Services/ (calculators + factory) with no UI — write a quick console test or xUnit test project to validate against Section 8's known values before touching any AXAML.
4. Implement ViewModelBase + RelayCommand (standard MVVM boilerplate, no external MVVM package).
5. Implement MainViewModel with all bindable properties and the Calculate command, still without AXAML (compile-check only).
6. Build MainWindow.axaml per Section 6, wire bindings to MainViewModel via `DataContext` in `MainWindow.axaml.cs`.
7. Implement the enum-friendly-name and numeric-formatting converters.
8. Wire up the model-type-driven `IsEnabled`/`IsVisible` rules from Section 5.
9. Run the app with `dotnet run` (works directly on CachyOS/Linux — no VM or Wine needed) and go through the full Testing Checklist (Section 8) manually.
10. Polish: decimal formatting, error banner styling, window sizing/resizing behavior.

---

## 10. Explicit Decisions Already Made (do not re-litigate)

- **UI framework: Avalonia, not WPF** — WPF is Windows-only and the target dev environment is Linux (CachyOS). Avalonia is the cross-platform, open-source equivalent with the same MVVM/XAML-based model.
- Poisson and Exponential are one merged UI option, not two.
- Time unit selector uses RadioButtons, despite original phrasing saying "checkboxes" (mutually exclusive choice = radio buttons is the correct control).
- Gamma's scale parameter is derived from the global rate (λ or μ) plus a user-entered shape k, rather than asking the user for scale directly — keeps λ/μ meaningful across all distribution choices.
- L, W, Wq share identical formulas/derivation across all three models; only Lq's formula differs per model. This is enforced structurally via `BaseQueueCalculator`.
- No external NuGet packages beyond the Avalonia project templates themselves — MVVM plumbing (ViewModelBase, RelayCommand) is hand-rolled rather than pulling in CommunityToolkit.Mvvm or ReactiveUI.