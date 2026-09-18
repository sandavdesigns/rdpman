using System.Text;

namespace RdpMan.Desktop;

internal sealed class AnsiTerminalBuffer
{
    private const int MaximumLines = 5000;
    private readonly List<StringBuilder> _lines = [new StringBuilder()];
    private readonly StringBuilder _pending = new();
    private int _row;
    private int _column;
    private int _savedRow;
    private int _savedColumn;

    public TerminalFrame Write(string text)
    {
        _pending.Append(text);
        var input = _pending.ToString();
        var consumed = 0;
        while (consumed < input.Length)
        {
            var character = input[consumed];
            if (character == '\x1b')
            {
                var escapeLength = EscapeSequenceLength(input, consumed);
                if (escapeLength == 0)
                {
                    break;
                }

                HandleEscape(input.AsSpan(consumed, escapeLength));
                consumed += escapeLength;
                continue;
            }

            HandleCharacter(character);
            consumed++;
        }

        _pending.Clear();
        if (consumed < input.Length)
        {
            _pending.Append(input.AsSpan(consumed));
        }

        TrimScrollback();
        return Render();
    }

    private void HandleCharacter(char character)
    {
        switch (character)
        {
            case '\r':
                _column = 0;
                return;
            case '\n':
                _row++;
                EnsureRow(_row);
                return;
            case '\b':
                _column = Math.Max(0, _column - 1);
                return;
            case '\t':
                _column = ((_column / 8) + 1) * 8;
                EnsureColumn(_lines[_row], _column);
                return;
            case '\0':
            case '\a':
                return;
        }

        if (char.IsControl(character))
        {
            return;
        }

        var line = _lines[_row];
        EnsureColumn(line, _column);
        if (_column < line.Length)
        {
            line[_column] = character;
        }
        else
        {
            line.Append(character);
        }
        _column++;
    }

    private void HandleEscape(ReadOnlySpan<char> sequence)
    {
        if (sequence.Length < 2)
        {
            return;
        }

        if (sequence[1] == '[')
        {
            HandleCsi(sequence[2..^1], sequence[^1]);
            return;
        }

        switch (sequence[1])
        {
            case '7':
                SaveCursor();
                break;
            case '8':
                RestoreCursor();
                break;
            case 'D':
                _row++;
                EnsureRow(_row);
                break;
            case 'E':
                _row++;
                _column = 0;
                EnsureRow(_row);
                break;
            case 'M':
                _row = Math.Max(0, _row - 1);
                break;
        }
    }

    private void HandleCsi(ReadOnlySpan<char> parameterSpan, char command)
    {
        var parameters = ParseParameters(parameterSpan);
        var first = Parameter(parameters, 0, 1);
        switch (command)
        {
            case 'A':
                _row = Math.Max(0, _row - first);
                break;
            case 'B':
                _row += first;
                EnsureRow(_row);
                break;
            case 'C':
                _column += first;
                break;
            case 'D':
                _column = Math.Max(0, _column - first);
                break;
            case 'E':
                _row += first;
                _column = 0;
                EnsureRow(_row);
                break;
            case 'F':
                _row = Math.Max(0, _row - first);
                _column = 0;
                break;
            case 'G':
            case '`':
                _column = Math.Max(0, first - 1);
                break;
            case 'H':
            case 'f':
                _row = Math.Max(0, Parameter(parameters, 0, 1) - 1);
                _column = Math.Max(0, Parameter(parameters, 1, 1) - 1);
                EnsureRow(_row);
                break;
            case 'J':
                EraseDisplay(Parameter(parameters, 0, 0));
                break;
            case 'K':
                EraseLine(Parameter(parameters, 0, 0));
                break;
            case 'P':
                DeleteCharacters(first);
                break;
            case '@':
                InsertCharacters(first);
                break;
            case 'X':
                EraseCharacters(first);
                break;
            case 's':
                SaveCursor();
                break;
            case 'u':
                RestoreCursor();
                break;
        }
    }

    private void EraseLine(int mode)
    {
        var line = _lines[_row];
        switch (mode)
        {
            case 1:
                var count = Math.Min(line.Length, _column + 1);
                line.Remove(0, count);
                _column = 0;
                break;
            case 2:
                line.Clear();
                _column = 0;
                break;
            default:
                if (_column < line.Length)
                {
                    line.Remove(_column, line.Length - _column);
                }
                break;
        }
    }

    private void EraseDisplay(int mode)
    {
        if (mode is 2 or 3)
        {
            _lines.Clear();
            _lines.Add(new StringBuilder());
            _row = 0;
            _column = 0;
            return;
        }

        if (mode == 1)
        {
            for (var index = 0; index < _row; index++)
            {
                _lines[index].Clear();
            }
            EraseLine(1);
            return;
        }

        EraseLine(0);
        while (_lines.Count > _row + 1)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }
    }

    private void DeleteCharacters(int count)
    {
        var line = _lines[_row];
        if (_column < line.Length)
        {
            line.Remove(_column, Math.Min(count, line.Length - _column));
        }
    }

    private void InsertCharacters(int count)
    {
        var line = _lines[_row];
        EnsureColumn(line, _column);
        line.Insert(_column, new string(' ', count));
    }

    private void EraseCharacters(int count)
    {
        var line = _lines[_row];
        EnsureColumn(line, _column + count);
        for (var index = _column; index < Math.Min(line.Length, _column + count); index++)
        {
            line[index] = ' ';
        }
    }

    private void SaveCursor()
    {
        _savedRow = _row;
        _savedColumn = _column;
    }

    private void RestoreCursor()
    {
        _row = Math.Clamp(_savedRow, 0, _lines.Count - 1);
        _column = Math.Max(0, _savedColumn);
    }

    private void EnsureRow(int row)
    {
        while (_lines.Count <= row)
        {
            _lines.Add(new StringBuilder());
        }
    }

    private static void EnsureColumn(StringBuilder line, int column)
    {
        while (line.Length < column)
        {
            line.Append(' ');
        }
    }

    private void TrimScrollback()
    {
        if (_lines.Count <= MaximumLines)
        {
            return;
        }

        var remove = _lines.Count - MaximumLines;
        _lines.RemoveRange(0, remove);
        _row = Math.Max(0, _row - remove);
        _savedRow = Math.Max(0, _savedRow - remove);
    }

    private TerminalFrame Render()
    {
        var text = string.Join(Environment.NewLine, _lines.Select(line => line.ToString()));
        var cursor = 0;
        for (var index = 0; index < _row; index++)
        {
            cursor += _lines[index].Length + Environment.NewLine.Length;
        }
        cursor += Math.Min(_column, _lines[_row].Length);
        return new TerminalFrame(text, cursor);
    }

    private static int EscapeSequenceLength(string input, int start)
    {
        if (start + 1 >= input.Length)
        {
            return 0;
        }

        if (input[start + 1] == '[')
        {
            for (var index = start + 2; index < input.Length; index++)
            {
                if (input[index] is >= '@' and <= '~')
                {
                    return index - start + 1;
                }
            }
            return 0;
        }

        if (input[start + 1] == ']')
        {
            for (var index = start + 2; index < input.Length; index++)
            {
                if (input[index] == '\a')
                {
                    return index - start + 1;
                }
                if (input[index] == '\x1b' && index + 1 < input.Length && input[index + 1] == '\\')
                {
                    return index - start + 2;
                }
            }
            return 0;
        }

        return 2;
    }

    private static int[] ParseParameters(ReadOnlySpan<char> span)
    {
        var text = span.ToString().TrimStart('?', '>', '!');
        if (text.Length == 0)
        {
            return [];
        }

        return text.Split(';')
            .Select(value => int.TryParse(value, out var parsed) ? parsed : 0)
            .ToArray();
    }

    private static int Parameter(int[] parameters, int index, int defaultValue)
    {
        if (index >= parameters.Length || parameters[index] == 0)
        {
            return defaultValue;
        }
        return parameters[index];
    }
}

internal sealed record TerminalFrame(string Text, int CursorIndex);
