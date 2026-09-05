using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KizaBit.Settings;

namespace KizaBit;

/// <summary>
/// KizaBit メインウィンドウ
/// 8-bit コンピュータのエミュレータ画面および操作UIを制御します。
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>CPU実行用タイマー</summary>
    private readonly DispatcherTimer cpuTimer;

    /// <summary>仮想マシンインスタンス</summary>
    private readonly VirtualMachine machine;

    /// <summary>アプリケーション設定オブジェクト</summary>
    private readonly AppSettings appSettings;

    public MainWindow()
    {
        InitializeComponent();

        // アプリケーション設定の読み込みとウィンドウ位置・サイズの復元
        appSettings = SettingsService.LoadSettings();
        SettingsService.ApplyWindowPlacement(this, appSettings);

        // 仮想マシンおよびCPUタイマーの初期化
        machine = new VirtualMachine();
        cpuTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(120)
        };
        cpuTimer.Tick += CpuTimer_Tick;

        // イベントハンドラーの登録
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    /// <summary>
    /// ウィンドウ読み込み完了時の処理
    /// 仮想マシンを起動し、初期画面を描画します。また初期設定ファイルを保存します。
    /// </summary>
    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        machine.Boot();
        UpdateView();
        CommandTextBox.Focus();

        // 初回起動時にも設定ファイルが存在するように保存
        SettingsService.RecordWindowPlacement(this, appSettings);
        SettingsService.SaveSettings(appSettings);
    }

    /// <summary>
    /// ウィンドウ終了時の処理
    /// ウィンドウ位置・サイズを設定ファイルに保存します。
    /// </summary>
    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        cpuTimer.Stop();
        SettingsService.RecordWindowPlacement(this, appSettings);
        SettingsService.SaveSettings(appSettings);
    }

    /// <summary>
    /// コマンド入力テキストボックスでEnterキーが押された時の処理
    /// </summary>
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

    /// <summary>
    /// リセットボタン押下時の処理
    /// </summary>
    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        cpuTimer.Stop();
        RunToggleButton.IsChecked = false;
        machine.Reset();
        UpdateView();
    }

    /// <summary>
    /// デモプログラム読み込みボタン押下時の処理
    /// </summary>
    private void DemoButton_Click(object sender, RoutedEventArgs e)
    {
        machine.LoadDemoProgram();
        UpdateView();
    }

    /// <summary>
    /// CPUステップ実行ボタン押下時の処理
    /// </summary>
    private void StepButton_Click(object sender, RoutedEventArgs e)
    {
        machine.StepCpu();
        UpdateView();
    }

    /// <summary>
    /// CPU連続実行トグルON時の処理
    /// </summary>
    private void RunToggleButton_Checked(object sender, RoutedEventArgs e)
    {
        cpuTimer.Start();
    }

    /// <summary>
    /// CPU連続実行トグルOFF時の処理
    /// </summary>
    private void RunToggleButton_Unchecked(object sender, RoutedEventArgs e)
    {
        cpuTimer.Stop();
    }

    /// <summary>
    /// CPUタイマーの周期処理
    /// </summary>
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

    /// <summary>
    /// CRT画面ビューポートのサイズ変更時にフォントサイズを再調整
    /// </summary>
    private void ScreenViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateScreenFontSize();
    }

    /// <summary>
    /// CRT画面およびCPUステータス表示を最新状態に更新します。
    /// </summary>
    private void UpdateView()
    {
        ScreenTextBlock.Text = machine.Display.Render();
        UpdateScreenFontSize();
        CpuStateTextBlock.Text = machine.Cpu.GetStateSummary();
    }

    /// <summary>
    /// 画面サイズと表示文字数（桁・行）に合わせてフォントサイズを動的に調整します。
    /// </summary>
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