using System.Windows.Input;
using System.Windows;
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

    private void UpdateView()
    {
        ScreenTextBlock.Text = machine.Display.Render();
        CpuStateTextBlock.Text = machine.Cpu.GetStateSummary();
    }
}