using System.Globalization;
using System.Runtime.CompilerServices;
using QueuingCalculator.Models;
using QueuingCalculator.Services;

namespace QueuingCalculator.ViewModels;

// Inputs for one side of the queue (arrivals or service): the distribution, its parameters,
// and the derived statistics σ², σ, C², C — any of which the user may type in directly.
// The mean is tied to the rate (mean = 1/rate), so typing a mean sets λ or μ instead.
public class DistributionInputViewModel : ViewModelBase
{
    private readonly Func<double> _rate;
    private readonly Action<string> _setRateText;
    private readonly string _side;
    private bool _refreshing;
    private bool _editingMean;

    public DistributionInputViewModel(string side, string timeNoun, string subscript, string rateSymbol,
        Func<double> rate, Action<string> setRateText, string uniformA, string uniformB, string normalSigma)
    {
        _side = side;
        _rate = rate;
        _setRateText = setRateText;
        _uniformAText = uniformA;
        _uniformBText = uniformB;
        _normalSigmaText = normalSigma;

        Title = $"{side} Distribution";
        UniformALabel = $"Min {timeNoun} time (a)";
        UniformBLabel = $"Max {timeNoun} time (b)";
        VarianceLabel = $"σ{subscript}²";
        StdDevLabel = $"σ{subscript}";
        ScvLabel = $"C{subscript}²";
        CvLabel = $"C{subscript}";
        MeanLabel = $"Mean (1/{rateSymbol})";

        ResetCommand = new RelayCommand(ClearOverride);
        RefreshStats();
    }

    // Raised when anything that affects the calculation changes.
    public event Action? Changed;

    public string Title { get; }
    public string UniformALabel { get; }
    public string UniformBLabel { get; }
    public string VarianceLabel { get; }
    public string StdDevLabel { get; }
    public string ScvLabel { get; }
    public string CvLabel { get; }
    public string MeanLabel { get; }

    public IReadOnlyList<DistributionType> DistributionTypes { get; } = Enum.GetValues<DistributionType>();
    public RelayCommand ResetCommand { get; }

    private string _timeUnitLabel = "";
    public string TimeUnitLabel
    {
        get => _timeUnitLabel;
        set
        {
            SetField(ref _timeUnitLabel, value);
        }
    }

    private string _lockedHint = "";
    public string LockedHint { get => _lockedHint; set => SetField(ref _lockedHint, value); }

    // ---- Editability (driven by the selected queue model) ----------------------------------

    private bool _isEditable = true;
    public bool IsEditable
    {
        get => _isEditable;
        set
        {
            if (!SetField(ref _isEditable, value)) return;
            if (!value) ClearOverride();
            OnPropertyChanged(nameof(ParamsEditable));
            OnPropertyChanged(nameof(ShowLockedHint));
        }
    }

    public bool ParamsEditable => IsEditable && !IsOverridden;
    public bool ShowLockedHint => !IsEditable;

    // ---- Distribution type and parameters --------------------------------------------------

    private DistributionType _selectedType = DistributionType.PoissonExponential;
    public DistributionType SelectedType
    {
        get => _selectedType;
        set
        {
            if (!SetField(ref _selectedType, value)) return;
            OnPropertyChanged(nameof(ShowUniformParams));
            OnPropertyChanged(nameof(ShowGammaParams));
            OnPropertyChanged(nameof(ShowNormalParams));
            OnPropertyChanged(nameof(ShowWeibullParams));
            OnPropertyChanged(nameof(IsMeanEditable));
            ClearOverride(); // a new distribution means the user wants its statistics
            RefreshStats();
            Changed?.Invoke();
        }
    }

    public bool ShowUniformParams => SelectedType == DistributionType.Uniform;
    public bool ShowGammaParams => SelectedType == DistributionType.Gamma;
    public bool ShowNormalParams => SelectedType == DistributionType.Normal;
    public bool ShowWeibullParams => SelectedType == DistributionType.Weibull;

    private string _uniformAText;
    public string UniformAText { get => _uniformAText; set => SetParam(ref _uniformAText, value); }

    private string _uniformBText;
    public string UniformBText { get => _uniformBText; set => SetParam(ref _uniformBText, value); }

    private string _gammaKText = "2";
    public string GammaKText { get => _gammaKText; set => SetParam(ref _gammaKText, value); }

    private string _normalSigmaText;
    public string NormalSigmaText { get => _normalSigmaText; set => SetParam(ref _normalSigmaText, value); }

    private string _weibullKText = "2";
    public string WeibullKText { get => _weibullKText; set => SetParam(ref _weibullKText, value); }

    // ---- Derived statistics (editable) -----------------------------------------------------

    private StatKind? _overrideKind;
    private string _overrideText = "";

    public bool IsOverridden => _overrideKind is not null;

    private string _varianceText = "", _stdDevText = "", _scvText = "", _cvText = "", _meanText = "";
    public string VarianceText { get => _varianceText; set => SetStat(StatKind.Variance, ref _varianceText, value); }
    public string StdDevText { get => _stdDevText; set => SetStat(StatKind.StdDev, ref _stdDevText, value); }
    public string ScvText { get => _scvText; set => SetStat(StatKind.Scv, ref _scvText, value); }
    public string CvText { get => _cvText; set => SetStat(StatKind.Cv, ref _cvText, value); }

    // Uniform's mean is fixed by (a+b)/2; every other distribution's mean is 1/rate and can be typed in.
    public bool IsMeanEditable => SelectedType != DistributionType.Uniform;

    public string MeanText
    {
        get => _meanText;
        set
        {
            value ??= "";
            if (_meanText == value) return;
            _meanText = value;
            if (_refreshing || !IsMeanEditable) return;

            double mean = Parse(value);
            _editingMean = true; // the rate change refreshes the stats; leave this box as typed
            try
            {
                _setRateText(mean > 0 ? Format(1 / mean) : "");
            }
            finally
            {
                _editingMean = false;
            }
        }
    }

    // ---- Building and validation -----------------------------------------------------------

    public DistributionParams Build()
    {
        var p = DistributionStatsHelper.Build(SelectedType, _rate(),
            Parse(UniformAText), Parse(UniformBText), Parse(GammaKText), Parse(NormalSigmaText), Parse(WeibullKText));

        if (_overrideKind is { } kind)
            p.VarianceOverride = DistributionStatsHelper.VarianceFrom(kind, Parse(_overrideText), p.Mean);
        return p;
    }

    public string? Validate() => DistributionStatsHelper.Validate(Build(), _side);

    // Recomputes the statistic boxes from the current inputs. The box the user typed into is left alone.
    public void RefreshStats()
    {
        var p = Build();
        double mean = p.Mean, variance = p.Variance;
        bool ok = Validate() is null && double.IsFinite(mean) && mean > 0 && double.IsFinite(variance) && variance >= 0;

        _refreshing = true;
        try
        {
            string meanText = double.IsFinite(mean) && mean > 0 ? Format(mean) : "";
            if (!_editingMean && _meanText != meanText)
            {
                _meanText = meanText;
                OnPropertyChanged(nameof(MeanText));
            }
            WriteStat(StatKind.Variance, nameof(VarianceText), ref _varianceText, ok ? variance : double.NaN);
            WriteStat(StatKind.StdDev, nameof(StdDevText), ref _stdDevText, ok ? Math.Sqrt(variance) : double.NaN);
            WriteStat(StatKind.Scv, nameof(ScvText), ref _scvText, ok ? variance / (mean * mean) : double.NaN);
            WriteStat(StatKind.Cv, nameof(CvText), ref _cvText, ok ? Math.Sqrt(variance) / mean : double.NaN);
        }
        finally
        {
            _refreshing = false;
        }
    }

    // ---- Helpers ---------------------------------------------------------------------------

    private void SetParam(ref string field, string? value, [CallerMemberName] string? name = null)
    {
        if (!SetField(ref field, value ?? "", name)) return;
        RefreshStats();
        Changed?.Invoke();
    }

    // Only a user edit reaches here with _refreshing == false: it becomes the override source.
    private void SetStat(StatKind kind, ref string field, string? value, [CallerMemberName] string? name = null)
    {
        value ??= "";
        if (field == value) return;
        field = value;
        if (_refreshing) return;

        _overrideKind = kind;
        _overrideText = value;
        OnPropertyChanged(nameof(IsOverridden));
        OnPropertyChanged(nameof(ParamsEditable));
        RefreshStats();
        Changed?.Invoke();
    }

    private void WriteStat(StatKind kind, string name, ref string field, double value)
    {
        if (_overrideKind == kind) return; // don't rewrite the box being typed in
        string text = double.IsNaN(value) ? "" : Format(value);
        if (field == text) return;
        field = text;
        OnPropertyChanged(name);
    }

    private void ClearOverride()
    {
        if (_overrideKind is null) return;
        _overrideKind = null;
        _overrideText = "";
        OnPropertyChanged(nameof(IsOverridden));
        OnPropertyChanged(nameof(ParamsEditable));
        RefreshStats();
        Changed?.Invoke();
    }

    private static string Format(double value) => value.ToString("G6", CultureInfo.InvariantCulture);

    internal static double Parse(string text)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v)
            ? v
            : double.NaN;
}
