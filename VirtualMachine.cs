namespace KizaBit;

public sealed class VirtualMachine
{
    private readonly BasicInterpreter basic;

    public VirtualMachine()
    {
        Display = new DisplayBuffer(80, 25);
        Cpu = new M6809Cpu();
        basic = new BasicInterpreter(Display, Cpu);

        LoadCpuMonitor();
    }

    public DisplayBuffer Display { get; }

    public M6809Cpu Cpu { get; }

    public void Boot()
    {
        Display.Clear();
        Cpu.Reset();
        LoadCpuMonitor();
        Display.WriteLine("KizaBit 8-BIT COMPUTER");
        Display.WriteLine("MOTOROLA 6809 CPU 64K RAM");
        Display.WriteLine("MICROSOFT BASIC STYLE INTERPRETER");
        Display.WriteLine();
        Display.WriteLine("READY.");
    }

    private CancellationTokenSource? currentCommandCts;

    /// <summary>
    /// BASICプログラムが現在実行中かどうかを取得します。
    /// </summary>
    public bool IsBasicRunning => currentCommandCts != null;

    /// <summary>
    /// 現在実行中のBASICプログラムを安全に中断（BREAK）します。
    /// </summary>
    public void Break()
    {
        currentCommandCts?.Cancel();
    }

    public void Reset()
    {
        Break();
        basic.ClearProgram();
        Boot();
    }

    /// <summary>
    /// コマンドを同期的に実行します。
    /// </summary>
    public void SubmitCommand(string command)
    {
        SubmitCommandAsync(command).GetAwaiter().GetResult();
    }

    /// <summary>
    /// コマンドを非同期に実行します。実行中の画面更新コールバックに対応します。
    /// </summary>
    public async Task SubmitCommandAsync(string command, Action? onYield = null)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            Display.WriteLine();
            Display.WriteLine("READY.");
            return;
        }

        Display.WriteLine($"> {command.Trim()}");

        using var cts = new CancellationTokenSource();
        currentCommandCts = cts;

        try
        {
            await basic.ProcessInputAsync(command, cts.Token, onYield);
        }
        finally
        {
            currentCommandCts = null;
        }
    }

    public void LoadDemoProgram()
    {
        basic.ClearProgram();
        basic.ProcessInput("10 CLS");
        basic.ProcessInput("20 PRINT \"KIZABIT BASIC DEMO\"");
        basic.ProcessInput("30 LET A = 1");
        basic.ProcessInput("40 PRINT \"COUNT=\" + A");
        basic.ProcessInput("50 LET A = A + 1");
        basic.ProcessInput("60 IF A <= 5 THEN 40");
        basic.ProcessInput("70 PRINT \"DONE\"");
        Display.WriteLine("DEMO PROGRAM LOADED.");
        Display.WriteLine("READY.");
    }

    public void StepCpu()
    {
        Cpu.Step();
    }

    private void LoadCpuMonitor()
    {
        Cpu.LoadProgram(0x0000, new byte[]
        {
            0x86, 0x00,
            0x4C,
            0x7E, 0x00, 0x02
        });
    }
}