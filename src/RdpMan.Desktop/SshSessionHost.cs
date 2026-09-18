using System.Text;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace RdpMan.Desktop;

public sealed class SshSessionHost : IRemoteSessionHost
{
    private readonly Panel _host = new();
    private readonly Panel _toolbar = new();
    private readonly RichTextBox _terminal = new();
    private readonly Button _copyButton = new();
    private readonly Button _pasteButton = new();
    private readonly ContextMenuStrip _terminalMenu = new();
    private readonly object _writeLock = new();
    private readonly Action<string> _rememberHostKey;
    private readonly AnsiTerminalBuffer _terminalBuffer = new();
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

        ConfigureToolbar();
        ConfigureContextMenu();

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
        _terminal.SelectionChanged += (_, _) => UpdateClipboardActions();
        _terminal.MouseDown += (_, _) => _terminal.Focus();
        _terminal.ContextMenuStrip = _terminalMenu;

        _host.Controls.Add(_terminal);
        _host.Controls.Add(_toolbar);
        UpdateClipboardActions();
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
        var terminalModes = new Dictionary<TerminalModes, uint>
        {
            [TerminalModes.VERASE] = 127,
            [TerminalModes.VINTR] = 3,
            [TerminalModes.IUTF8] = 1,
        };
        _shell = _client.CreateShellStream(
            "xterm-256color",
            columns,
            rows,
            (uint)Math.Max(1, _terminal.ClientSize.Width),
            (uint)Math.Max(1, _terminal.ClientSize.Height),
            16 * 1024,
            terminalModes);
        _shell.DataReceived += ShellDataReceived;
        AppendSystemLine("SSH-Verbindung hergestellt. Strg+Umschalt+V fügt Text ein.");
        UpdateClipboardActions();
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

        if (!_disposed)
        {
            UpdateClipboardActions();
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
        _terminalMenu.Dispose();
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
        if (string.IsNullOrEmpty(text) || _terminal.IsDisposed)
        {
            return;
        }

        void Append()
        {
            if (_terminal.IsDisposed)
            {
                return;
            }

            var frame = _terminalBuffer.Write(text);
            _terminal.Text = frame.Text;
            _terminal.SelectionStart = Math.Clamp(frame.CursorIndex, 0, _terminal.TextLength);
            _terminal.SelectionLength = 0;
            _terminal.SelectionColor = _terminal.ForeColor;
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
            CopySelection();
            e.SuppressKeyPress = true;
            return;
        }

        if ((e.Control && e.Shift && e.KeyCode == Keys.V) || (e.Shift && e.KeyCode == Keys.Insert))
        {
            PasteClipboard();
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

    private void ConfigureToolbar()
    {
        _toolbar.Dock = DockStyle.Top;
        _toolbar.Height = 42;
        _toolbar.BackColor = Color.FromArgb(15, 23, 42);
        _toolbar.Padding = new Padding(8, 6, 8, 6);
        _toolbar.Paint += (_, e) =>
        {
            using var border = new Pen(Color.FromArgb(51, 65, 85));
            e.Graphics.DrawLine(border, 0, _toolbar.Height - 1, _toolbar.Width, _toolbar.Height - 1);
        };

        ConfigureToolbarButton(_copyButton, "Kopieren", 8, CopySelection);
        ConfigureToolbarButton(_pasteButton, "Einfügen", 104, PasteClipboard);
        _toolbar.Controls.Add(_copyButton);
        _toolbar.Controls.Add(_pasteButton);
    }

    private static void ConfigureToolbarButton(Button button, string text, int left, Action action)
    {
        button.Text = text;
        button.Size = new Size(90, 29);
        button.Location = new Point(left, 6);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(71, 85, 105);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(51, 65, 85);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(71, 85, 105);
        button.BackColor = Color.FromArgb(30, 41, 59);
        button.ForeColor = Color.FromArgb(226, 232, 240);
        button.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        button.Cursor = Cursors.Hand;
        button.Click += (_, _) => action();
    }

    private void ConfigureContextMenu()
    {
        var copy = _terminalMenu.Items.Add("Kopieren", null, (_, _) => CopySelection());
        var paste = _terminalMenu.Items.Add("Einfügen", null, (_, _) => PasteClipboard());
        _terminalMenu.Opening += (_, _) =>
        {
            copy.Enabled = _terminal.SelectionLength > 0;
            paste.Enabled = ClipboardHasText();
        };
    }

    private void UpdateClipboardActions()
    {
        _copyButton.Enabled = _terminal.SelectionLength > 0;
        _pasteButton.Enabled = IsConnected;
    }

    private void CopySelection()
    {
        if (_terminal.SelectionLength > 0)
        {
            _terminal.Copy();
        }
        _terminal.Focus();
    }

    private void PasteClipboard()
    {
        try
        {
            if (IsConnected && ClipboardHasText())
            {
                var text = Clipboard.GetText().Replace("\r\n", "\r").Replace('\n', '\r');
                Write(text);
            }
        }
        catch
        {
            // The Windows clipboard can be temporarily locked by another application.
        }
        _terminal.Focus();
    }

    private static bool ClipboardHasText()
    {
        try
        {
            return Clipboard.ContainsText();
        }
        catch
        {
            return false;
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

}
