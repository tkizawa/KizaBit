namespace KizaBit;

public sealed class Z80Cpu
{
    private readonly byte[] memory = new byte[ushort.MaxValue + 1];

    public byte A { get; private set; }
    public byte F { get; private set; }
    public byte B { get; private set; }
    public byte C { get; private set; }
    public byte D { get; private set; }
    public byte E { get; private set; }
    public byte H { get; private set; }
    public byte L { get; private set; }
    public ushort PC { get; private set; }
    public ushort SP { get; private set; }
    public bool Halted { get; private set; }
    public string LastInstruction { get; private set; } = "RESET";

    public void Reset()
    {
        A = 0;
        F = 0;
        B = 0;
        C = 0;
        D = 0;
        E = 0;
        H = 0;
        L = 0;
        PC = 0;
        SP = 0xFFFF;
        Halted = false;
        LastInstruction = "RESET";
    }

    public void LoadProgram(ushort startAddress, IReadOnlyList<byte> bytes)
    {
        for (var index = 0; index < bytes.Count; index++)
        {
            memory[startAddress + index] = bytes[index];
        }

        PC = startAddress;
        Halted = false;
        LastInstruction = $"LOAD @{startAddress:X4}";
    }

    public void Step()
    {
        if (Halted)
        {
            LastInstruction = "HALTED";
            return;
        }

        var opcode = FetchByte();

        switch (opcode)
        {
            case 0x00:
                LastInstruction = "NOP";
                break;
            case 0x06:
                B = FetchByte();
                LastInstruction = $"LD B,{B:X2}";
                UpdateZeroFlag(B);
                break;
            case 0x0E:
                C = FetchByte();
                LastInstruction = $"LD C,{C:X2}";
                UpdateZeroFlag(C);
                break;
            case 0x21:
            {
                var value = FetchWord();
                H = (byte)(value >> 8);
                L = (byte)(value & 0xFF);
                LastInstruction = $"LD HL,{value:X4}";
                break;
            }
            case 0x31:
                SP = FetchWord();
                LastInstruction = $"LD SP,{SP:X4}";
                break;
            case 0x32:
            {
                var address = FetchWord();
                memory[address] = A;
                LastInstruction = $"LD ({address:X4}),A";
                break;
            }
            case 0x3A:
            {
                var address = FetchWord();
                A = memory[address];
                LastInstruction = $"LD A,({address:X4})";
                UpdateZeroFlag(A);
                break;
            }
            case 0x3C:
                A++;
                LastInstruction = "INC A";
                UpdateZeroFlag(A);
                break;
            case 0x3D:
                A--;
                LastInstruction = "DEC A";
                UpdateZeroFlag(A);
                break;
            case 0x3E:
                A = FetchByte();
                LastInstruction = $"LD A,{A:X2}";
                UpdateZeroFlag(A);
                break;
            case 0x76:
                Halted = true;
                LastInstruction = "HALT";
                break;
            case 0x80:
                A = unchecked((byte)(A + B));
                LastInstruction = "ADD A,B";
                UpdateZeroFlag(A);
                break;
            case 0x81:
                A = unchecked((byte)(A + C));
                LastInstruction = "ADD A,C";
                UpdateZeroFlag(A);
                break;
            case 0xAF:
                A = 0;
                LastInstruction = "XOR A";
                UpdateZeroFlag(A);
                break;
            case 0xC3:
            {
                var address = FetchWord();
                PC = address;
                LastInstruction = $"JP {address:X4}";
                break;
            }
            default:
                Halted = true;
                LastInstruction = $"UNSUPPORTED {opcode:X2}";
                break;
        }
    }

    public string GetStateSummary()
    {
        return string.Join(Environment.NewLine,
        [
            $"PC   {PC:X4}",
            $"SP   {SP:X4}",
            $"AF   {A:X2}{F:X2}",
            $"BC   {B:X2}{C:X2}",
            $"DE   {D:X2}{E:X2}",
            $"HL   {H:X2}{L:X2}",
            $"Z    {(IsZeroFlagSet() ? 1 : 0)}",
            $"RUN  {(Halted ? "HALT" : "ACTIVE")}",
            string.Empty,
            "LAST INSTR",
            LastInstruction
        ]);
    }

    private byte FetchByte()
    {
        return memory[PC++];
    }

    private ushort FetchWord()
    {
        var low = FetchByte();
        var high = FetchByte();
        return (ushort)(low | (high << 8));
    }

    private void UpdateZeroFlag(byte value)
    {
        if (value == 0)
        {
            F |= 0x40;
            return;
        }

        F &= 0xBF;
    }

    private bool IsZeroFlagSet()
    {
        return (F & 0x40) != 0;
    }
}