using QueuingCalculator.Models;
using QueuingCalculator.Services;

namespace QueuingCalculator.ViewModels;

public class MainViewModel : ViewModelBase
{
    public MainViewModel()
    {
        Arrival = new DistributionInputViewModel("Arrival", "interarrival", "ₐ", "λ", () => Lambda, t => LambdaText = t,
            uniformA: "0.4", uniformB: "0.6", normalSigma: "0.1");
        Service = new DistributionInputViewModel("Service", "service", "ₛ", "μ", () => Mu, t => MuText = t,
            uniformA: "0.1", uniformB: "0.3", normalSigma: "0.05");

        Arrival.Changed += OnInputsChanged;
        Service.Changed += OnInputsChanged;

        CalculateCommand = new RelayCommand(Calculate, () => ValidationMessage is null);
        ApplyModelRules();
        ApplyTimeUnit();
    }

    public IReadOnlyList<QueueModelType> ModelTypes { get; } = Enum.GetValues<QueueModelType>();

    public DistributionInputViewModel Arrival { get; }
    public DistributionInputViewModel Service { get; }

    public RelayCommand CalculateCommand { get; }

    // ---- Model selection -------------------------------------------------------------------

    private QueueModelType _selectedModel = QueueModelType.MM1;
    public QueueModelType SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (!SetField(ref _selectedModel, value)) return;
            ApplyModelRules();
            OnInputsChanged();
        }
    }

    // M/M/1: both sides exponential. M/G/1: arrivals exponential, service free. G/G/1: both free.
    private void ApplyModelRules()
    {
        Arrival.IsEditable = SelectedModel == QueueModelType.GG1;
        Service.IsEditable = SelectedModel != QueueModelType.MM1;

        if (!Arrival.IsEditable) Arrival.SelectedType = DistributionType.PoissonExponential;
        if (!Service.IsEditable) Service.SelectedType = DistributionType.PoissonExponential;

        Arrival.LockedHint = "Fixed to Poisson / Exponential for M/M/1 and M/G/1.";
        Service.LockedHint = "Fixed to Poisson / Exponential for M/M/1.";
    }

    // ---- Rates -----------------------------------------------------------------------------
    // Inputs are bound as text so that blank or malformed entries are reported, not thrown.

    private string _lambdaText = "2";
    public string LambdaText
    {
        get => _lambdaText;
        set
        {
            if (!SetField(ref _lambdaText, value ?? "")) return;
            Arrival.RefreshStats();
            OnInputsChanged();
        }
    }
    public double Lambda => DistributionInputViewModel.Parse(LambdaText);

    private string _muText = "5";
    public string MuText
    {
        get => _muText;
        set
        {
            if (!SetField(ref _muText, value ?? "")) return;
            Service.RefreshStats();
            OnInputsChanged();
        }
    }
    public double Mu => DistributionInputViewModel.Parse(MuText);

    // ---- Time unit -------------------------------------------------------------------------
    // Rates are entered "per unit", so the unit only relabels inputs and time-valued outputs.

    private TimeUnit _selectedTimeUnit = TimeUnit.Minutes;
    public TimeUnit SelectedTimeUnit
    {
        get => _selectedTimeUnit;
        set
        {
            if (!SetField(ref _selectedTimeUnit, value)) return;
            OnPropertyChanged(nameof(IsSeconds));
            OnPropertyChanged(nameof(IsMinutes));
            OnPropertyChanged(nameof(IsHours));
            OnPropertyChanged(nameof(TimeUnitLabel));
            OnPropertyChanged(nameof(RateUnitLabel));
            ApplyTimeUnit();
        }
    }

    public bool IsSeconds { get => SelectedTimeUnit == TimeUnit.Seconds; set { if (value) SelectedTimeUnit = TimeUnit.Seconds; } }
    public bool IsMinutes { get => SelectedTimeUnit == TimeUnit.Minutes; set { if (value) SelectedTimeUnit = TimeUnit.Minutes; } }
    public bool IsHours { get => SelectedTimeUnit == TimeUnit.Hours; set { if (value) SelectedTimeUnit = TimeUnit.Hours; } }

    public string TimeUnitLabel => SelectedTimeUnit switch
    {
        TimeUnit.Seconds => "sec",
        TimeUnit.Minutes => "min",
        TimeUnit.Hours => "hr",
        _ => ""
    };

    public string RateUnitLabel => $"per {TimeUnitLabel}";

    private void ApplyTimeUnit()
    {
        Arrival.TimeUnitLabel = TimeUnitLabel;
        Service.TimeUnitLabel = TimeUnitLabel;
    }

    // ---- Validation ------------------------------------------------------------------------

    public string? ValidationMessage
    {
        get
        {
            if (!(Lambda > 0)) return "Arrival rate λ must be a number greater than 0.";
            if (!(Mu > 0)) return "Service rate μ must be a number greater than 0.";
            return Arrival.Validate() ?? Service.Validate();
        }
    }

    public bool HasValidationMessage => ValidationMessage is not null;

    // ---- Results ---------------------------------------------------------------------------

    private double? _rho, _rho0, _l, _lq, _w, _wq, _caSquared, _csSquared, _serviceVariance;
    public double? Rho { get => _rho; private set => SetField(ref _rho, value); }
    public double? Rho0 { get => _rho0; private set => SetField(ref _rho0, value); }
    public double? L { get => _l; private set => SetField(ref _l, value); }
    public double? Lq { get => _lq; private set => SetField(ref _lq, value); }
    public double? W { get => _w; private set => SetField(ref _w, value); }
    public double? Wq { get => _wq; private set => SetField(ref _wq, value); }

    // The distribution statistics the formulas actually used.
    public double? CaSquared { get => _caSquared; private set => SetField(ref _caSquared, value); }
    public double? CsSquared { get => _csSquared; private set => SetField(ref _csSquared, value); }
    public double? ServiceVariance { get => _serviceVariance; private set => SetField(ref _serviceVariance, value); }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetField(ref _errorMessage, value))
                OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => ErrorMessage is not null;

    private void Calculate()
    {
        if (ValidationMessage is not null) return;

        var arrivalParams = Arrival.Build();
        var serviceParams = Service.Build();
        var calculator = QueueCalculatorFactory.Create(SelectedModel);
        var result = calculator.Calculate(Lambda, Mu, arrivalParams, serviceParams);

        if (result.ErrorMessage is not null)
        {
            ClearResults();
            ErrorMessage = result.ErrorMessage;
            return;
        }

        ErrorMessage = null;
        Rho = result.Rho;
        Rho0 = result.Rho0;
        L = result.L;
        Lq = result.Lq;
        W = result.W;
        Wq = result.Wq;
        CaSquared = arrivalParams.SCV;
        CsSquared = serviceParams.SCV;
        ServiceVariance = serviceParams.Variance;
    }

    // ---- Helpers ---------------------------------------------------------------------------

    // Any input change invalidates the displayed results and may change validity.
    private void OnInputsChanged()
    {
        ClearResults();
        ErrorMessage = null;
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(HasValidationMessage));
        CalculateCommand?.RaiseCanExecuteChanged();
    }

    private void ClearResults()
    {
        Rho = Rho0 = L = Lq = W = Wq = CaSquared = CsSquared = ServiceVariance = null;
    }
}
