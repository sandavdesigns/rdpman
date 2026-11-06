namespace RdpMan.Desktop;

internal sealed class GraphicsStateScope(Graphics graphics) : IDisposable
{
    private readonly System.Drawing.Drawing2D.GraphicsState _state = graphics.Save();
    public void Dispose() => graphics.Restore(_state);
}

internal sealed class MachineListBox : ListBox
{
    public event EventHandler? ViewChanged;

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        // Native scrolling also occurs through keyboard navigation and selection.
        if (m.Msg is 0x115 or 0x20A or 0x100 or 0x197 or 0x185)
            ViewChanged?.Invoke(this, EventArgs.Empty);
    }
}

internal sealed class MachineScrollBar : Control
{
    private readonly ListBox _list;
    private bool _hover;
    private int _dragOffset;
    private int Scale(int value) => (int)Math.Round(value * DeviceDpi / 96f);
    private int VisibleRows => Math.Max(1, _list.ClientSize.Height / Math.Max(1, _list.ItemHeight));
    private int Maximum => Math.Max(0, _list.Items.Count - VisibleRows);
    private Rectangle Thumb
    {
        get
        {
            var height = Math.Min(Height, Math.Max(Scale(28), Height * VisibleRows / Math.Max(1, _list.Items.Count)));
            var top = Maximum == 0 ? 0 : (Height - height) * Math.Min(_list.TopIndex, Maximum) / Maximum;
            var width = Scale(_hover || Capture ? 6 : 4);
            return new Rectangle((Width - width) / 2, top, width, height);
        }
    }

    public MachineScrollBar(MachineListBox list)
    {
        _list = list;
        BackColor = AppTheme.Sidebar;
        TabStop = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        list.ViewChanged += (_, _) => Invalidate();
        list.SelectedIndexChanged += (_, _) => Invalidate();
        list.DataSourceChanged += (_, _) => Invalidate();
        list.SizeChanged += (_, _) => Invalidate();
        AccessibleName = "Serverliste scrollen";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Maximum == 0 || Height <= 0) return;
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(_hover || Capture ? Color.FromArgb(148, 163, 184) : Color.FromArgb(80, 105, 130));
        e.Graphics.FillRoundedRectangle(brush, Thumb, Scale(2));
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || Maximum == 0) return;
        _dragOffset = Thumb.Contains(e.Location) ? e.Y - Thumb.Top : Thumb.Height / 2;
        Capture = true;
        ScrollTo(e.Y);
    }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (Capture) ScrollTo(e.Y); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); Capture = false; Invalidate(); }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        _list.TopIndex = Math.Clamp(_list.TopIndex - Math.Sign(e.Delta) * 3, 0, Maximum);
        Invalidate();
    }
    private void ScrollTo(int y)
    {
        var travel = Height - Thumb.Height;
        if (travel > 0) _list.TopIndex = (int)Math.Round(Math.Clamp((y - _dragOffset) / (double)travel, 0, 1) * Maximum);
        Invalidate();
    }
}
