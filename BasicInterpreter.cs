using System.Globalization;
using System.Text.RegularExpressions;

namespace KizaBit;

public sealed class BasicInterpreter
{
    private static readonly Regex LineRegex = new("^(\\d+)\\s*(.*)$", RegexOptions.Compiled);

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
                ExecuteStatement(input, null, out _);
                return;
        }
    }

    private void ShowHelp()
    {
        display.WriteLine("COMMANDS: NEW LIST RUN CLS HELP CPU");
        display.WriteLine("STATEMENTS: PRINT LET IF THEN GOTO END REM");
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

        var pointer = 0;
        while (pointer < lines.Length)
        {
            ExecuteStatement(lines[pointer].Value, lineIndexMap, out var nextLine);

            if (nextLine is null)
            {
                pointer++;
                continue;
            }

            if (!lineIndexMap.TryGetValue(nextLine.Value, out pointer))
            {
                throw new BasicRuntimeException("UNDEFINED LINE NUMBER");
            }
        }
    }

    private void ExecuteStatement(string statement, Dictionary<int, int>? lineIndexMap, out int? nextLine)
    {
        nextLine = null;
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
            if (lineIndexMap is not null)
            {
                nextLine = int.MaxValue;
            }

            return;
        }

        if (upper == "CLS")
        {
            display.Clear();
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
            nextLine = ParseLineNumber(trimmed[5..]);
            return;
        }

        if (upper.StartsWith("IF ", StringComparison.Ordinal))
        {
            ExecuteIf(trimmed[3..], out nextLine);
            return;
        }

        if (trimmed.Contains('=') && !trimmed.Contains("THEN", StringComparison.OrdinalIgnoreCase))
        {
            ExecuteAssignment(trimmed);
            return;
        }

        throw new BasicRuntimeException("SYNTAX ERROR");
    }

    private void ExecuteIf(string expression, out int? nextLine)
    {
        nextLine = null;
        var thenIndex = expression.IndexOf("THEN", StringComparison.OrdinalIgnoreCase);
        if (thenIndex < 0)
        {
            throw new BasicRuntimeException("SYNTAX ERROR");
        }

        var condition = expression[..thenIndex].Trim();
        var target = expression[(thenIndex + 4)..].Trim();
        if (EvaluateCondition(condition))
        {
            nextLine = ParseLineNumber(target);
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

    private sealed class BasicRuntimeException(string message) : Exception(message)
    {
    }
}