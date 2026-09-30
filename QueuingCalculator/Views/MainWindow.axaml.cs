using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using QueuingCalculator.ViewModels;

namespace QueuingCalculator.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();

        // Reject non-numeric keystrokes in TextBoxes marked with the "numeric" class.
        // Pasted text bypasses this; the ViewModel's validation catches that case.
        AddHandler(TextInputEvent, OnNumericTextInput, RoutingStrategies.Tunnel);
    }

    private static void OnNumericTextInput(object? sender, TextInputEventArgs e)
    {
        var textBox = (e.Source as Control)?.FindAncestorOfType<TextBox>(includeSelf: true);
        if (textBox is null || !textBox.Classes.Contains("numeric") || e.Text is null) return;

        if (!e.Text.All(c => char.IsAsciiDigit(c) || c is '.' or '-' or '+' or 'e' or 'E'))
            e.Handled = true;
    }
}
