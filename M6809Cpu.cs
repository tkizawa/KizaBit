namespace KizaBit;

public sealed class M6809Cpu
{
    private const byte CarryFlag = 0x01;
    private const byte OverflowFlag = 0x02;
    private const byte ZeroFlag = 0x04;
    private const byte NegativeFlag = 0x08;

    private readonly byte[] memory = new byte[ushort.MaxValue + 1];

    public byte A { get; private set; }
    public byte B { get; private set; }
    public byte DP { get; private set; }
    public byte CC { get; private set; }
    public ushort X { get; private set; }
    public ushort Y { get; private set; }
    public ushort U { get; private set; }
    public ushort S { get; private set; }
    public ushort PC { get; private set; }
    public bool Halted { get; private set; }
    public string LastInstruction { get; private set; } = "RESET";

    public void Reset()
    {
        A = 0;
        B = 0;
        DP = 0;
        CC = 0;
        X = 0;
        Y = 0;
        U = 0;
        S = 0xFFFF;
        PC = 0;
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

        ExecuteOpcode(FetchByte());
    }

    public string GetStateSummary()
    {
        return string.Join(Environment.NewLine,
        [
            $"PC   {PC:X4}",
            $"S    {S:X4}",
            $"U    {U:X4}",
            $"D    {A:X2}{B:X2}",
            $"X    {X:X4}",
            $"Y    {Y:X4}",
            $"DP   {DP:X2}",
            $"CC   {CC:X2}",
            $"RUN  {(Halted ? "HALT" : "ACTIVE")}",
            string.Empty,
            "LAST INSTR",
            LastInstruction
        ]);
    }

    private void ExecuteOpcode(byte opcode)
    {
        switch (opcode)
        {
            case 0x12:
                LastInstruction = "NOP";
                break;
            case 0x20:
            {
                var offset = unchecked((sbyte)FetchByte());
                PC = (ushort)(PC + offset);
                LastInstruction = $"BRA {offset:+#;-#;0}";
                break;
            }
            case 0x26:
            {
                var offset = unchecked((sbyte)FetchByte());
                if (!IsFlagSet(ZeroFlag))
                {
                    PC = (ushort)(PC + offset);
                }

                LastInstruction = $"BNE {offset:+#;-#;0}";
                break;
            }
            case 0x27:
            {
                var offset = unchecked((sbyte)FetchByte());
                if (IsFlagSet(ZeroFlag))
                {
                    PC = (ushort)(PC + offset);
                }

                LastInstruction = $"BEQ {offset:+#;-#;0}";
                break;
            }
            case 0x3F:
                Halted = true;
                LastInstruction = "SWI";
                break;
            case 0x4A:
                A = unchecked((byte)(A - 1));
                UpdateNzFlags(A);
                LastInstruction = "DECA";
                break;
            case 0x4C:
                A = unchecked((byte)(A + 1));
                UpdateNzFlags(A);
                LastInstruction = "INCA";
                break;
            case 0x4F:
                A = 0;
                UpdateNzFlags(A);
                ClearFlag(OverflowFlag | CarryFlag);
                LastInstruction = "CLRA";
                break;
            case 0x7E:
            {
                var address = FetchWord();
                PC = address;
                LastInstruction = $"JMP ${address:X4}";
                break;
            }
            case 0x86:
                A = FetchByte();
                UpdateNzFlags(A);
                LastInstruction = $"LDA #${A:X2}";
                break;
            case 0x8B:
            {
                var value = FetchByte();
                var result = A + value;
                A = unchecked((byte)result);
                UpdateAddFlags((byte)(result & 0xFF), result > byte.MaxValue);
                LastInstruction = $"ADDA #${value:X2}";
                break;
            }
            case 0x8E:
                X = FetchWord();
                UpdateNzFlags(X);
                LastInstruction = $"LDX #${X:X4}";
                break;
            case 0xB6:
            {
                var address = FetchWord();
                A = memory[address];
                UpdateNzFlags(A);
                LastInstruction = $"LDA ${address:X4}";
                break;
            }
            case 0xB7:
            {
                var address = FetchWord();
                memory[address] = A;
                UpdateNzFlags(A);
                LastInstruction = $"STA ${address:X4}";
                break;
            }
            case 0xC6:
                B = FetchByte();
                UpdateNzFlags(B);
                LastInstruction = $"LDB #${B:X2}";
                break;
            case 0x10:
                ExecutePageTwoOpcode();
                break;
            default:
                Halted = true;
                LastInstruction = $"UNSUPPORTED {opcode:X2}";
                break;
        }
    }

    private void ExecutePageTwoOpcode()
    {
        var opcode = FetchByte();

        switch (opcode)
        {
            case 0x8E:
                Y = FetchWord();
                UpdateNzFlags(Y);
                LastInstruction = $"LDY #${Y:X4}";
                break;
            case 0xCE:
                S = FetchWord();
                UpdateNzFlags(S);
                LastInstruction = $"LDS #${S:X4}";
                break;
            default:
                Halted = true;
                LastInstruction = $"UNSUPPORTED 10 {opcode:X2}";
                break;
        }
    }

    private byte FetchByte()
    {
        return memory[PC++];
    }

    private ushort FetchWord()
    {
        var high = FetchByte();
        var low = FetchByte();
        return (ushort)((high << 8) | low);
    }

    private void UpdateNzFlags(byte value)
    {
        UpdateFlag(ZeroFlag, value == 0);
        UpdateFlag(NegativeFlag, (value & 0x80) != 0);
    }

    private void UpdateNzFlags(ushort value)
    {
        UpdateFlag(ZeroFlag, value == 0);
        UpdateFlag(NegativeFlag, (value & 0x8000) != 0);
    }

    private void UpdateAddFlags(byte result, bool carry)
    {
        UpdateNzFlags(result);
        UpdateFlag(CarryFlag, carry);
        ClearFlag(OverflowFlag);
    }

    private bool IsFlagSet(byte flag)
    {
        return (CC & flag) != 0;
    }

    private void UpdateFlag(byte flag, bool isSet)
    {
        if (isSet)
        {
            CC |= flag;
            return;
        }

        CC &= unchecked((byte)~flag);
    }

    private void ClearFlag(byte flag)
    {
        CC &= unchecked((byte)~flag);
    }
}