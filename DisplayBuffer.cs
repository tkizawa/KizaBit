using System.Text;

namespace KizaBit;

public sealed class DisplayBuffer
{
    private char[,] cells;
    private int cursorColumn;
    private int cursorRow;

    public DisplayBuffer(int columns, int rows)
    {
        Columns = columns;
        Rows = rows;
        cells = new char[rows, columns];
        Clear();
    }

    public int Columns { get; private set; }

    public int Rows { get; private set; }

    public void Resize(int columns, int rows)
    {
        if (columns <= 0 || rows <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columns), "Display dimensions must be positive.");
        }

        Columns = columns;
        Rows = rows;
        cells = new char[rows, columns];
        Clear();
    }

    public void Clear()
    {
        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                cells[row, column] = ' ';
            }
        }

        cursorColumn = 0;
        cursorRow = 0;
    }

    public void Write(string text)
    {
        foreach (var character in text)
        {
            if (character == '\r')
            {
                continue;
            }

            if (character == '\n')
            {
                NewLine();
                continue;
            }

            if (cursorColumn >= Columns)
            {
                NewLine();
            }

            cells[cursorRow, cursorColumn] = character;
            cursorColumn++;
        }
    }

    public void WriteLine(string text = "")
    {
        Write(text);
        NewLine();
    }

    public string Render()
    {
        var builder = new StringBuilder((Columns + 1) * Rows);

        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                builder.Append(cells[row, column]);
            }

            if (row < Rows - 1)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    private void NewLine()
    {
        cursorColumn = 0;
        cursorRow++;

        if (cursorRow < Rows)
        {
            return;
        }

        ScrollUp();
        cursorRow = Rows - 1;
    }

    private void ScrollUp()
    {
        for (var row = 1; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                cells[row - 1, column] = cells[row, column];
            }
        }

        for (var column = 0; column < Columns; column++)
        {
            cells[Rows - 1, column] = ' ';
        }
    }
}