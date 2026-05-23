using System.Globalization;
using System.Text.RegularExpressions;

namespace KizaBit;

public sealed class BasicInterpreter
{
    private static readonly Regex LineRegex = new("^(\\d+)\\s*(.*)$", RegexOptions.Compiled);
    private static readonly Regex ForRegex = new("^([A-Za-z])\\s*=\\s*(.+?)\\s+TO\\s+(.+?)(?:\\s+STEP\\s+(.+))?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly DisplayBuffer display;
    private readonly SortedDictionary<int, string> program = new();
    private readonly Dictionary<string, int> variables = new(StringComparer.OrdinalIgnoreCase);
    private readonly M6809Cpu cpu;

    public BasicInterpreter(DisplayBuffer display, M6809Cpu cpu)
    {
        this.display = display;
        this.cpu = cpu;
    }

    public void ClearProgram()
    {
        program.Clear();
        variables.Clear();
    }

    public void ProcessInput(string input)
    {
        try
        {
            var trimmed = input.Trim();
            if (TryStoreProgramLine(trimmed))
            {
                display.WriteLine("READY.");
                return;
            }

            ExecuteImmediate(trimmed);
            display.WriteLine("READY.");
        }
        catch (BasicRuntimeException exception)
        {
            display.WriteLine(exception.Message);
            display.WriteLine("READY.");
        }
    }

    private bool TryStoreProgramLine(string input)
    {
        var match = LineRegex.Match(input);
        if (!match.Success)
        {
            return false;
        }

        var lineNumber = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var lineText = match.Groups[2].Value.Trim();

        if (string.IsNullOrEmpty(lineText))
        {
            program.Remove(lineNumber);
        }
        else
        {
            program[lineNumber] = lineText;
        }

        return true;
    }

    private void ExecuteImmediate(string input)
    {
        var upper = input.ToUpperInvariant();

        switch (upper)
        {
            case "":
                return;
            case "NEW":
                ClearProgram();
                display.WriteLine("OK");
                return;
            case "LIST":
                ListProgram();
                return;
            case "RUN":
                RunProgram();
                return;
            case "CLS":
                display.Clear();
                return;
            case "HELP":
                ShowHelp();
                return;
            case "CPU":
                display.WriteLine(cpu.GetStateSummary().Replace(Environment.NewLine, " "));
                return;
            default:
                ExecuteStatement(input, null);
                return;
        }
    }

    private void ShowHelp()
    {
        display.WriteLine("COMMANDS: NEW LIST RUN CLS WIDTH HELP CPU");
        display.WriteLine("STATEMENTS: PRINT LET IF THEN GOTO FOR NEXT GOSUB RETURN END REM");
    }

    private void ListProgram()
    {
        foreach (var line in program)
        {
            display.WriteLine($"{line.Key} {line.Value}");
        }
    }

    private void RunProgram()
    {
        variables.Clear();

        var lines = program.ToArray();
        var lineIndexMap = lines
            .Select((line, index) => new { line.Key, index })
            .ToDictionary(item => item.Key, item => item.index);

        var context = new ExecutionContext(lines, lineIndexMap);

        while (context.Pointer < lines.Length && !context.IsTerminated)
        {
            ExecuteStatement(lines[context.Pointer].Value, context);

            if (context.IsTerminated)
            {
                continue;
            }

            if (context.NextIndex is int nextIndex)
            {
                context.Pointer = nextIndex;
                context.NextIndex = null;
                continue;
            }

            context.Pointer++;
        }
    }

    private void ExecuteStatement(string statement, ExecutionContext? context)
    {
        var trimmed = statement.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        var upper = trimmed.ToUpperInvariant();
        if (upper.StartsWith("REM", StringComparison.Ordinal))
        {
            return;
        }

        if (upper == "END")
        {
            if (context is not null)
            {
                context.Terminate();
            }

            return;
        }

        if (upper == "CLS")
        {
            display.Clear();
            return;
        }

        if (upper.StartsWith("WIDTH", StringComparison.Ordinal))
        {
            ExecuteWidth(trimmed[5..]);
            return;
        }

        if (upper.StartsWith("PRINT", StringComparison.Ordinal))
        {
            var value = trimmed.Length > 5 ? trimmed[5..].Trim() : string.Empty;
            display.WriteLine(EvaluatePrintValue(value));
            return;
        }

        if (upper.StartsWith("LET ", StringComparison.Ordinal))
        {
            ExecuteAssignment(trimmed[4..]);
            return;
        }

        if (upper.StartsWith("GOTO ", StringComparison.Ordinal))
        {
            RequireProgramContext(context);
            context!.JumpToLine(ParseLineNumber(trimmed[5..]));
            return;
        }

        if (upper.StartsWith("GOSUB ", StringComparison.Ordinal))
        {
            RequireProgramContext(context);
            context!.PushReturnAddress(context.Pointer + 1);
            context.JumpToLine(ParseLineNumber(trimmed[6..]));
            return;
        }

        if (upper == "RETURN")
        {
            RequireProgramContext(context);
            context!.ReturnFromSubroutine();
            return;
        }

        if (upper.StartsWith("IF ", StringComparison.Ordinal))
        {
            ExecuteIf(trimmed[3..], context);
            return;
        }

        if (upper.StartsWith("FOR ", StringComparison.Ordinal))
        {
            RequireProgramContext(context);
            ExecuteFor(trimmed[4..], context!);
            return;
        }

        if (upper.StartsWith("NEXT", StringComparison.Ordinal))
        {
            RequireProgramContext(context);
            ExecuteNext(trimmed.Length > 4 ? trimmed[4..] : string.Empty, context!);
            return;
        }

        if (trimmed.Contains('=') && !trimmed.Contains("THEN", StringComparison.OrdinalIgnoreCase))
        {
            ExecuteAssignment(trimmed);
            return;
        }

        throw new BasicRuntimeException("SYNTAX ERROR");
    }

    private void ExecuteIf(string expression, ExecutionContext? context)
    {
        var thenIndex = expression.IndexOf("THEN", StringComparison.OrdinalIgnoreCase);
        if (thenIndex < 0)
        {
            throw new BasicRuntimeException("SYNTAX ERROR");
        }

        var condition = expression[..thenIndex].Trim();
        var target = expression[(thenIndex + 4)..].Trim();
        if (EvaluateCondition(condition))
        {
            RequireProgramContext(context);
            context!.JumpToLine(ParseLineNumber(target));
        }
    }

    private void ExecuteWidth(string expression)
    {
        var args = expression.Trim();
        if (args.Length == 0)
        {
            throw new BasicRuntimeException("SYNTAX ERROR");
        }

        var parts = args
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length is < 1 or > 2)
        {
            throw new BasicRuntimeException("SYNTAX ERROR");
        }

        var columns = EvaluateNumericExpression(parts[0]);
        var rows = parts.Length == 2 ? EvaluateNumericExpression(parts[1]) : display.Rows;
        if (columns <= 0 || rows <= 0)
        {
            throw new BasicRuntimeException("BAD SCREEN SIZE");
        }

        display.Resize(columns, rows);
    }

    private void ExecuteFor(string expression, ExecutionContext context)
    {
        var match = ForRegex.Match(expression);
        if (!match.Success)
        {
            throw new BasicRuntimeException("SYNTAX ERROR");
        }

        var variableName = match.Groups[1].Value;
        var startValue = EvaluateNumericExpression(match.Groups[2].Value);
        var endValue = EvaluateNumericExpression(match.Groups[3].Value);
        var stepValue = match.Groups[4].Success ? EvaluateNumericExpression(match.Groups[4].Value) : 1;

        if (stepValue == 0)
        {
            throw new BasicRuntimeException("STEP CANNOT BE ZERO");
        }

        variables[variableName] = startValue;

        if (!ShouldContinueLoop(startValue, endValue, stepValue))
        {
            context.JumpToIndex(context.FindLoopExitIndex(context.Pointer + 1));
            return;
        }

        context.PushLoop(new ForFrame(variableName, endValue, stepValue, context.Pointer + 1));
    }

    private void ExecuteNext(string expression, ExecutionContext context)
    {
        var variableName = expression.Trim();
        var frame = context.PeekLoop();
        if (frame is null)
        {
            throw new BasicRuntimeException("NEXT WITHOUT FOR");
        }

        if (variableName.Length > 0)
        {
            if (variableName.Length != 1 || !char.IsLetter(variableName[0]))
            {
                throw new BasicRuntimeException("BAD VARIABLE NAME");
            }

            if (!string.Equals(frame.VariableName, variableName, StringComparison.OrdinalIgnoreCase))
            {
                throw new BasicRuntimeException("NEXT WITHOUT FOR");
            }
        }

        var nextValue = variables.TryGetValue(frame.VariableName, out var currentValue)
            ? currentValue + frame.StepValue
            : frame.StepValue;
        variables[frame.VariableName] = nextValue;

        if (ShouldContinueLoop(nextValue, frame.EndValue, frame.StepValue))
        {
            context.JumpToIndex(frame.LoopStartIndex);
            return;
        }

        context.PopLoop();
    }

    private static bool ShouldContinueLoop(int value, int endValue, int stepValue)
    {
        return stepValue >= 0 ? value <= endValue : value >= endValue;
    }

    private static void RequireProgramContext(ExecutionContext? context)
    {
        if (context is null)
        {
            throw new BasicRuntimeException("ILLEGAL DIRECT");
        }
    }

    private void ExecuteAssignment(string assignment)
    {
        var equalsIndex = assignment.IndexOf('=');
        if (equalsIndex <= 0)
        {
            throw new BasicRuntimeException("SYNTAX ERROR");
        }

        var variableName = assignment[..equalsIndex].Trim();
        if (variableName.Length != 1 || !char.IsLetter(variableName[0]))
        {
            throw new BasicRuntimeException("BAD VARIABLE NAME");
        }

        var value = EvaluateNumericExpression(assignment[(equalsIndex + 1)..]);
        variables[variableName] = value;
    }

    private string EvaluatePrintValue(string expression)
    {
        if (string.IsNullOrEmpty(expression))
        {
            return string.Empty;
        }

        var segments = expression.Split('+', StringSplitOptions.TrimEntries);
        var rendered = new List<string>();

        foreach (var segment in segments)
        {
            if (segment.StartsWith('"') && segment.EndsWith('"') && segment.Length >= 2)
            {
                rendered.Add(segment[1..^1]);
                continue;
            }

            rendered.Add(EvaluateNumericExpression(segment).ToString(CultureInfo.InvariantCulture));
        }

        return string.Concat(rendered);
    }

    private bool EvaluateCondition(string condition)
    {
        foreach (var op in new[] { "<=", ">=", "<>", "=", "<", ">" })
        {
            var parts = condition.Split(op, StringSplitOptions.None);
            if (parts.Length != 2)
            {
                continue;
            }

            var left = EvaluateNumericExpression(parts[0]);
            var right = EvaluateNumericExpression(parts[1]);

            return op switch
            {
                "=" => left == right,
                "<>" => left != right,
                "<" => left < right,
                ">" => left > right,
                "<=" => left <= right,
                ">=" => left >= right,
                _ => false
            };
        }

        throw new BasicRuntimeException("SYNTAX ERROR");
    }

    private int EvaluateNumericExpression(string expression)
    {
        var tokenizer = new ExpressionTokenizer(expression, variables);
        return ParseExpression(tokenizer);
    }

    private static int ParseExpression(ExpressionTokenizer tokenizer)
    {
        var value = ParseTerm(tokenizer);

        while (tokenizer.CurrentToken is "+" or "-")
        {
            var op = tokenizer.CurrentToken;
            tokenizer.MoveNext();
            var right = ParseTerm(tokenizer);
            value = op == "+" ? value + right : value - right;
        }

        return value;
    }

    private static int ParseTerm(ExpressionTokenizer tokenizer)
    {
        var value = ParseFactor(tokenizer);

        while (tokenizer.CurrentToken is "*" or "/")
        {
            var op = tokenizer.CurrentToken;
            tokenizer.MoveNext();
            var right = ParseFactor(tokenizer);
            value = op == "*" ? value * right : value / right;
        }

        return value;
    }

    private static int ParseFactor(ExpressionTokenizer tokenizer)
    {
        if (tokenizer.CurrentToken == "(")
        {
            tokenizer.MoveNext();
            var value = ParseExpression(tokenizer);
            tokenizer.Expect(")");
            tokenizer.MoveNext();
            return value;
        }

        if (tokenizer.CurrentToken == "-")
        {
            tokenizer.MoveNext();
            return -ParseFactor(tokenizer);
        }

        if (int.TryParse(tokenizer.CurrentToken, CultureInfo.InvariantCulture, out var literal))
        {
            tokenizer.MoveNext();
            return literal;
        }

        if (tokenizer.CurrentToken.Length == 1 && char.IsLetter(tokenizer.CurrentToken[0]))
        {
            var variableName = tokenizer.CurrentToken;
            tokenizer.MoveNext();
            return tokenizer.ResolveVariable(variableName);
        }

        throw new BasicRuntimeException("SYNTAX ERROR");
    }

    private static int ParseLineNumber(string text)
    {
        if (!int.TryParse(text.Trim(), CultureInfo.InvariantCulture, out var lineNumber))
        {
            throw new BasicRuntimeException("SYNTAX ERROR");
        }

        return lineNumber;
    }

    private sealed class ExpressionTokenizer
    {
        private readonly string text;
        private readonly Dictionary<string, int> variables;
        private int position;

        public ExpressionTokenizer(string text, Dictionary<string, int> variables)
        {
            this.text = text;
            this.variables = variables;
            CurrentToken = string.Empty;
            MoveNext();
        }

        public string CurrentToken { get; private set; }

        public void MoveNext()
        {
            while (position < text.Length && char.IsWhiteSpace(text[position]))
            {
                position++;
            }

            if (position >= text.Length)
            {
                CurrentToken = string.Empty;
                return;
            }

            var current = text[position];
            if (char.IsDigit(current))
            {
                var start = position;
                while (position < text.Length && char.IsDigit(text[position]))
                {
                    position++;
                }

                CurrentToken = text[start..position];
                return;
            }

            if (char.IsLetter(current))
            {
                CurrentToken = text[position].ToString();
                position++;
                return;
            }

            CurrentToken = current.ToString();
            position++;
        }

        public void Expect(string token)
        {
            if (CurrentToken != token)
            {
                throw new BasicRuntimeException("SYNTAX ERROR");
            }
        }

        public int ResolveVariable(string variableName)
        {
            return variables.TryGetValue(variableName, out var value) ? value : 0;
        }
    }

    private sealed class ExecutionContext(
        KeyValuePair<int, string>[] lines,
        Dictionary<int, int> lineIndexMap)
    {
        private readonly Stack<ForFrame> loopStack = new();
        private readonly Stack<int> gosubStack = new();

        public int Pointer { get; set; }

        public int? NextIndex { get; set; }

        public bool IsTerminated { get; private set; }

        public void Terminate()
        {
            IsTerminated = true;
            NextIndex = null;
        }

        public void JumpToLine(int lineNumber)
        {
            if (!lineIndexMap.TryGetValue(lineNumber, out var index))
            {
                throw new BasicRuntimeException("UNDEFINED LINE NUMBER");
            }

            JumpToIndex(index);
        }

        public void JumpToIndex(int index)
        {
            NextIndex = index;
        }

        public void PushLoop(ForFrame frame)
        {
            loopStack.Push(frame);
        }

        public ForFrame? PeekLoop()
        {
            return loopStack.Count == 0 ? null : loopStack.Peek();
        }

        public void PopLoop()
        {
            loopStack.Pop();
        }

        public void PushReturnAddress(int returnIndex)
        {
            gosubStack.Push(returnIndex);
        }

        public void ReturnFromSubroutine()
        {
            if (gosubStack.Count == 0)
            {
                throw new BasicRuntimeException("RETURN WITHOUT GOSUB");
            }

            JumpToIndex(gosubStack.Pop());
        }

        public int FindLoopExitIndex(int searchStartIndex)
        {
            var nestedLoopDepth = 0;

            for (var index = searchStartIndex; index < lines.Length; index++)
            {
                var text = lines[index].Value.Trim();
                if (text.StartsWith("FOR ", StringComparison.OrdinalIgnoreCase))
                {
                    nestedLoopDepth++;
                    continue;
                }

                if (!text.StartsWith("NEXT", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (nestedLoopDepth == 0)
                {
                    return index + 1;
                }

                nestedLoopDepth--;
            }

            throw new BasicRuntimeException("FOR WITHOUT NEXT");
        }
    }

    private sealed class ForFrame(string variableName, int endValue, int stepValue, int loopStartIndex)
    {
        public string VariableName { get; } = variableName;

        public int EndValue { get; } = endValue;

        public int StepValue { get; } = stepValue;

        public int LoopStartIndex { get; } = loopStartIndex;
    }

    private sealed class BasicRuntimeException(string message) : Exception(message)
    {
    }
}