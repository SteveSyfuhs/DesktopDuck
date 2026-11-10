using System.Drawing.Drawing2D;

namespace DesktopDuck;

internal class DuckWindow : Form
{
    private readonly Bitmap standingFrame = DuckArt.CreateFrame(0, false);
    private readonly Bitmap walkingFrame = DuckArt.CreateFrame(1, false);
    private readonly Bitmap quackingFrame = DuckArt.CreateFrame(0, true);
    private int rotation;
    private bool isStepping;
    private bool isQuacking;
    private bool isFacingBackwards;

    public DuckWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.Manual;
    }

    public void UpdateDuck(EdgePose pose, Rectangle bounds, int size, bool stepping,
        bool quacking = false, bool backwards = false)
    {
        if (ClientSize.Width != size || ClientSize.Height != size)
        {
            ClientSize = new Size(size, size);
        }
        Location = new Point(bounds.Left + (int)Math.Round(pose.X),
            bounds.Top + (int)Math.Round(pose.Y));
        rotation = pose.Rotation;
        isStepping = stepping;
        isQuacking = quacking;
        isFacingBackwards = backwards;
        Invalidate();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ExStyle |= 0x08000000 | 0x00000080; // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
            return parameters;
        }
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x0021) // WM_MOUSEACTIVATE
        {
            message.Result = 3; // MA_NOACTIVATE
            return;
        }

        base.WndProc(ref message);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Bitmap frame = isQuacking ? quackingFrame : isStepping ? walkingFrame : standingFrame;
        Graphics graphics = e.Graphics;
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.TranslateTransform(ClientSize.Width / 2f, ClientSize.Height / 2f);
        graphics.RotateTransform(rotation);
        if (isFacingBackwards)
        {
            graphics.ScaleTransform(-1, 1);
        }
        graphics.TranslateTransform(-ClientSize.Width / 2f, -ClientSize.Height / 2f);
        graphics.DrawImage(frame, ClientRectangle);

        if (!TopMost)
        {
            TopMost = true;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            standingFrame.Dispose();
            walkingFrame.Dispose();
            quackingFrame.Dispose();
        }

        base.Dispose(disposing);
    }
}
