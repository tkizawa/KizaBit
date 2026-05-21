namespace KizaBit;

public sealed class VirtualMachine
{
    private readonly BasicInterpreter basic;

    public VirtualMachine()
    {
        Display = new DisplayBuffer(80, 25);
        Cpu = new Z80Cpu();
        basic = new BasicInterpreter(Display, Cpu);

        LoadCpuMonitor();
    }

    public DisplayBuffer Display { get; }

    public Z80Cpu Cpu { get; }

    public void Boot()
    {
        Display.Clear();
        Cpu.Reset();
        LoadCpuMonitor();
        Display.WriteLine("KizaBit 8-BIT COMPUTER");
        Display.WriteLine("Z80 CPU 64K RAM");
        Display.WriteLine("MICROSOFT BASIC STYLE INTERPRETER");
        Display.WriteLine();
        Display.WriteLine("READY.");
    }

    public void Reset()
    {
        basic.ClearProgram();
        Boot();
    }

    public void SubmitCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            Display.WriteLine();
            Display.WriteLine("READY.");
            return;
        }

        Display.WriteLine($"> {command.Trim()}" );
        basic.ProcessInput(command);
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
            0x3E, 0x00,
            0x3C,
            0xC3, 0x02, 0x00
        });
    }
}