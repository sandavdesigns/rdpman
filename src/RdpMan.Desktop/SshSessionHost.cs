using System.Text;
using System.Text.RegularExpressions;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace RdpMan.Desktop;

public sealed partial class SshSessionHost : IRemoteSessionHost
{
    private readonly Panel _host = new();
    private readonly RichTextBox _terminal = new();
    private readonly object _writeLock = new();
    private readonly Action<string> _rememberHostKey;
    private SshClient? _client;
    private ShellStream? _shell;
    private bool _disposed;

    public MachineEntry Machine { get; }
    public CredentialProfile? Credential { get; }
    public Control Control => _host;
    public bool IsConnected => !_disposed && _client?.IsConnected == true && _shell?.CanWrite == true;

    public SshSessionHost(MachineEntry machine, CredentialProfile? credential, Action<string> rememberHostKey, string textColor)
    {
        Machine = machine;
        Credential = credential;
        _rememberHostKey = rememberHostKey;

        _host.BackColor = Color.FromArgb(10, 15, 24);
        _host.Dock = DockStyle.Fill;

        _terminal.Dock = DockStyle.Fill;
        _terminal.ReadOnly = true;
        _terminal.BorderStyle = BorderStyle.None;
        _terminal.BackColor = Color.FromArgb(10, 15, 24);
        _terminal.ForeColor = SshTerminalTheme.ParseTextColor(textColor);
        _terminal.Font = new Font("Consolas", 11f, FontStyle.Regular);
        _terminal.DetectUrls = false;
        _terminal.HideSelection = false;
        _terminal.WordWrap = false;
        _terminal.AcceptsTab = true;
        _terminal.ShortcutsEnabled = false;
        _terminal.KeyDown += TerminalKeyDown;
        _terminal.KeyPress += TerminalKeyPress;
        _terminal.MouseDown += (_, _) => _terminal.Focus();

        _host.Controls.Add(_terminal);
    }

    public void SetTextColor(string textColor)
    {
        var color = SshTerminalTheme.ParseTextColor(textColor);
        _terminal.ForeColor = color;
        _terminal.SelectAll();
        _terminal.SelectionColor = color;
        _terminal.Select(_terminal.TextLength, 0);
        _terminal.SelectionColor = color;
        _terminal.Invalidate();
    }

    public void Connect()
    {
        if (IsConnected)
        {
            _terminal.Focus();
            return;
        }

        var username = Credential?.Username.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new InvalidOperationException("Für SSH muss ein Zugang mit Benutzername und Passwort ausgewählt sein.");
        }

        var password = string.IsNullOrWhiteSpace(Credential?.ProtectedPassword)
            ? ""
            : CredentialVault.Unprotect(Credential.ProtectedPassword);
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Der ausgewählte SSH-Zugang enthält kein Passwort.");
        }

        Disconnect();
        AppendSystemLine($"Verbinde per SSH mit {Machine.ConnectionHost}:{Machine.SshPort} als {username} ...");

        var authentication = new PasswordAuthenticationMethod(username, password);
        var connectionInfo = new ConnectionInfo(Machine.ConnectionHost, Machine.SshPort, username, authentication)
        {
            Timeout = TimeSpan.FromSeconds(15),
        };
        _client = new SshClient(connectionInfo)
        {
            KeepAliveInterval = TimeSpan.FromSeconds(30),
        };
        _client.HostKeyReceived += VerifyHostKey;
        _client.Connect();

        var (columns, rows) = TerminalSize();
        _shell = _client.CreateShellStream(
            "xterm-256color",
            columns,
            rows,
            (uint)Math.Max(1, _terminal.ClientSize.Width),
            (uint)Math.Max(1, _terminal.ClientSize.Height),
            16 * 1024);
        _shell.DataReceived += ShellDataReceived;
        AppendSystemLine("SSH-Verbindung hergestellt. Strg+Umschalt+V fügt Text ein.");
        _terminal.Focus();
    }

    public void Reconnect()
    {
        Disconnect();
        Connect();
    }

    public void Disconnect()
    {
        if (_shell is not null)
        {
            _shell.DataReceived -= ShellDataReceived;
            _shell.Dispose();
            _shell = null;
        }

        if (_client is not null)
        {
            if (_client.IsConnected)
            {
                _client.Disconnect();
            }
            _client.Dispose();
            _client = null;
        }
    }

    public void ResizeToHost()
    {
        _host.Dock = DockStyle.Fill;
        if (_shell is null || !_shell.CanWrite)
        {
            return;
        }

        var (columns, rows) = TerminalSize();
        try
        {
            _shell.ChangeWindowSize(
                columns,
                rows,
                (uint)Math.Max(1, _terminal.ClientSize.Width),
                (uint)Math.Max(1, _terminal.ClientSize.Height));
        }
        catch
        {
            // A resize can race with a remote disconnect.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Disconnect();
        _terminal.Dispose();
        _host.Dispose();
    }

    private void ShellDataReceived(object? sender, ShellDataEventArgs e)
    {
        if (_disposed || e.Data.Length == 0)
        {
            return;
        }

        AppendTerminalText(Encoding.UTF8.GetString(e.Data));
    }

    private void VerifyHostKey(object? sender, HostKeyEventArgs e)
    {
        var fingerprint = e.FingerPrintSHA256;
        var knownFingerprint = Machine.SshHostKeyFingerprint.Trim();
        if (!string.IsNullOrWhiteSpace(knownFingerprint)
            && string.Equals(knownFingerprint, fingerprint, StringComparison.Ordinal))
        {
            e.CanTrust = true;
            return;
        }

        var changed = !string.IsNullOrWhiteSpace(knownFingerprint);
        var message = changed
            ? $"WARNUNG: Der SSH-Hostschlüssel von '{Machine.DisplayName}' hat sich geändert.\n\nBisher: {knownFingerprint}\nNeu: {fingerprint}\n\nNur bestätigen, wenn der Schlüsselwechsel bekannt und geprüft ist."
            : $"Erste SSH-Verbindung zu '{Machine.DisplayName}'.\n\nHost-Fingerabdruck:\n{fingerprint}\n\nDiesem Server vertrauen?";
        var result = MessageBox.Show(
            _host.FindForm(),
            message,
            changed ? "SSH-Hostschlüssel geändert" : "SSH-Host bestätigen",
            MessageBoxButtons.YesNo,
            changed ? MessageBoxIcon.Warning : MessageBoxIcon.Question);

        e.CanTrust = result == DialogResult.Yes;
        if (e.CanTrust)
        {
            Machine.SshHostKeyFingerprint = fingerprint;
            _rememberHostKey(fingerprint);
        }
    }

    private void AppendSystemLine(string text)
    {
        AppendTerminalText($"\r\n[RDPMan] {text}\r\n");
    }

    private void AppendTerminalText(string text)
    {
        var clean = AnsiEscapeSequence().Replace(text, "").Replace("\r\n", "\n").Replace('\r', '\n');
        if (string.IsNullOrEmpty(clean) || _terminal.IsDisposed)
        {
            return;
        }

        void Append()
        {
            if (_terminal.IsDisposed)
            {
                return;
            }

            foreach (var character in clean)
            {
                if (character == '\b')
                {
                    if (_terminal.TextLength > 0)
                    {
                        _terminal.Select(_terminal.TextLength - 1, 1);
                        _terminal.SelectedText = "";
                    }
                    continue;
                }

                if (!char.IsControl(character) || character is '\n' or '\t')
                {
                    _terminal.AppendText(character.ToString());
                }
            }

            const int maximumCharacters = 500_000;
            if (_terminal.TextLength > maximumCharacters)
            {
                _terminal.Select(0, _terminal.TextLength - maximumCharacters);
                _terminal.SelectedText = "";
            }
            _terminal.SelectionStart = _terminal.TextLength;
            _terminal.ScrollToCaret();
        }

        if (_terminal.InvokeRequired)
        {
            _terminal.BeginInvoke((Action)Append);
        }
        else
        {
            Append();
        }
    }

    private void TerminalKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.Shift && e.KeyCode == Keys.C)
        {
            if (_terminal.SelectionLength > 0)
            {
                _terminal.Copy();
            }
            e.SuppressKeyPress = true;
            return;
        }

        if ((e.Control && e.Shift && e.KeyCode == Keys.V) || (e.Shift && e.KeyCode == Keys.Insert))
        {
            if (Clipboard.ContainsText())
            {
                Write(Clipboard.GetText().Replace("\r\n", "\r").Replace('\n', '\r'));
            }
            e.SuppressKeyPress = true;
            return;
        }

        var sequence = e.KeyCode switch
        {
            Keys.Enter => "\r",
            Keys.Back => "\x7f",
            Keys.Tab => "\t",
            Keys.Up => "\x1b[A",
            Keys.Down => "\x1b[B",
            Keys.Right => "\x1b[C",
            Keys.Left => "\x1b[D",
            Keys.Home => "\x1b[H",
            Keys.End => "\x1b[F",
            Keys.Delete => "\x1b[3~",
            Keys.PageUp => "\x1b[5~",
            Keys.PageDown => "\x1b[6~",
            _ when e.Control && e.KeyCode is >= Keys.A and <= Keys.Z => ((char)((int)e.KeyCode - (int)Keys.A + 1)).ToString(),
            _ => "",
        };
        if (sequence.Length == 0)
        {
            return;
        }

        Write(sequence);
        e.SuppressKeyPress = true;
    }

    private void TerminalKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar))
        {
            Write(e.KeyChar.ToString());
            e.Handled = true;
        }
    }

    private void Write(string text)
    {
        var shell = _shell;
        if (shell is null || !shell.CanWrite)
        {
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(text);
        lock (_writeLock)
        {
            shell.Write(bytes, 0, bytes.Length);
            shell.Flush();
        }
    }

    private (uint Columns, uint Rows) TerminalSize()
    {
        var characterWidth = Math.Max(1f, TextRenderer.MeasureText("M", _terminal.Font).Width);
        var characterHeight = Math.Max(1, _terminal.Font.Height);
        var columns = (uint)Math.Clamp((int)(_terminal.ClientSize.Width / characterWidth), 20, 500);
        var rows = (uint)Math.Clamp(_terminal.ClientSize.Height / characterHeight, 5, 300);
        return (columns, rows);
    }

    [GeneratedRegex("\\x1B(?:\\[[0-?]*[ -/]*[@-~]|\\][^\\a]*(?:\\a|\\x1B\\\\)|[@-_])", RegexOptions.Compiled)]
    private static partial Regex AnsiEscapeSequence();
}
