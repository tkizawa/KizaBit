using System.Globalization;
using System.Windows.Input;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace KizaBit;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer cpuTimer;
    private readonly VirtualMachine machine;

    public MainWindow()
    {
        InitializeComponent();

        machine = new VirtualMachine();
        cpuTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(120)
        };
        cpuTimer.Tick += CpuTimer_Tick;

        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        machine.Boot();
        UpdateView();
        CommandTextBox.Focus();
    }

    private void CommandTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        var command = CommandTextBox.Text;
        CommandTextBox.Clear();
        machine.SubmitCommand(command);
        UpdateView();
        e.Handled = true;
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        cpuTimer.Stop();
        RunToggleButton.IsChecked = false;
        machine.Reset();
        UpdateView();
    }

    private void DemoButton_Click(object sender, RoutedEventArgs e)
    {
        machine.LoadDemoProgram();
        UpdateView();
    }

    private void StepButton_Click(object sender, RoutedEventArgs e)
    {
        machine.StepCpu();
        UpdateView();
    }

    private void RunToggleButton_Checked(object sender, RoutedEventArgs e)
    {
        cpuTimer.Start();
    }

    private void RunToggleButton_Unchecked(object sender, RoutedEventArgs e)
    {
        cpuTimer.Stop();
    }

    private void CpuTimer_Tick(object? sender, EventArgs e)
    {
        machine.StepCpu();
        UpdateView();

        if (machine.Cpu.Halted)
        {
            cpuTimer.Stop();
            RunToggleButton.IsChecked = false;
        }
    }

    private void ScreenViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateScreenFontSize();
    }

    private void UpdateView()
    {
        ScreenTextBlock.Text = machine.Display.Render();
        UpdateScreenFontSize();
        CpuStateTextBlock.Text = machine.Cpu.GetStateSummary();
    }

    private void UpdateScreenFontSize()
    {
        if (!IsLoaded || ScreenViewport.ActualWidth <= 0 || ScreenViewport.ActualHeight <= 0)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var sample = new FormattedText(
            "W",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(
                ScreenTextBlock.FontFamily,
                ScreenTextBlock.FontStyle,
                ScreenTextBlock.FontWeight,
                ScreenTextBlock.FontStretch),
            100,
            Brushes.Transparent,
            dpi);

        var lineSpacing = ScreenTextBlock.FontFamily.LineSpacing;
        var widthPerEm = sample.WidthIncludingTrailingWhitespace / 100d;
        var maxWidthFont = ScreenViewport.ActualWidth / (machine.Display.Columns * widthPerEm);
        var maxHeightFont = ScreenViewport.ActualHeight / (machine.Display.Rows * lineSpacing);
        var fontSize = Math.Max(6d, Math.Floor(Math.Min(maxWidthFont, maxHeightFont)));

        ScreenTextBlock.FontSize = fontSize;
        ScreenTextBlock.LineHeight = fontSize * lineSpacing;
    }
}