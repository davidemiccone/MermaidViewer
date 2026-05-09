using System.Drawing.Drawing2D;

namespace MermaidViewer;

/// <summary>Iconcine vettoriali semplici, rasterizzate alla dimensione fisica corretta per il monitor corrente.</summary>
internal static class ToolbarImages
{
    public static int IconSizeFor(Control host)
    {
        var d = host.DeviceDpi;
        return Math.Clamp((int)Math.Round(16f * d / 96f), 16, 48);
    }

    public static void ApplyToButton(ToolStripButton btn, Image? image)
    {
        var old = btn.Image;
        btn.Image = image;
        old?.Dispose();
    }

    public static Image OpenFile(int s)
    {
        var bmp = NewBmp(s);
        using var g = Gfx(bmp);
        using var pen = Pen();
        var m = s * 0.12f;
        var w = s - 2 * m;
        // Cartella stilizzata
        var path = new GraphicsPath();
        path.AddPolygon(new[]
        {
            new PointF(m + w * 0.05f, m + w * 0.28f),
            new PointF(m + w * 0.38f, m + w * 0.28f),
            new PointF(m + w * 0.45f, m + w * 0.12f),
            new PointF(m + w * 0.95f, m + w * 0.12f),
            new PointF(m + w * 0.95f, m + w * 0.88f),
            new PointF(m + w * 0.05f, m + w * 0.88f),
        });
        path.CloseFigure();
        using var br = new SolidBrush(Color.FromArgb(255, 255, 220, 120));
        g.FillPath(br, path);
        g.DrawPath(pen, path);
        return bmp;
    }

    public static Image FitView(int s)
    {
        var bmp = NewBmp(s);
        using var g = Gfx(bmp);
        using var pen = Pen();
        var m = s * 0.18f;
        var x0 = m;
        var y0 = m;
        var x1 = s - m;
        var y1 = s - m;
        var inset = s * 0.22f;
        g.DrawRectangle(pen, x0, y0, x1 - x0, y1 - y0);
        // Frecce agli angoli (adatta alla finestra)
        var a = s * 0.12f;
        Arrow(g, pen, x0 + inset, y0 + a, x0 + a, y0 + a, x0 + a, y0 + inset);
        Arrow(g, pen, x1 - inset, y0 + a, x1 - a, y0 + a, x1 - a, y0 + inset);
        Arrow(g, pen, x0 + inset, y1 - a, x0 + a, y1 - a, x0 + a, y1 - inset);
        Arrow(g, pen, x1 - inset, y1 - a, x1 - a, y1 - a, x1 - a, y1 - inset);
        return bmp;
    }

    public static Image ZoomActual(int s)
    {
        var bmp = NewBmp(s);
        using var g = Gfx(bmp);
        using var pen = Pen(1.8f);
        var m = s * 0.22f;
        g.DrawEllipse(pen, m, m, s - 2 * m, s - 2 * m);
        using var font = new Font(FontFamily.GenericSansSerif, s * 0.28f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var br = new SolidBrush(Color.FromArgb(230, 40, 40, 40));
        g.DrawString("1:1", font, br, s * 0.28f, s * 0.32f);
        return bmp;
    }

    public static Image ZoomIn(int s)
    {
        var bmp = NewBmp(s);
        using var g = Gfx(bmp);
        using var pen = Pen();
        var c = s / 2f;
        var r = s * 0.22f;
        g.DrawEllipse(pen, c - r, c - r, 2 * r, 2 * r);
        g.DrawLine(pen, c - r * 0.45f, c, c + r * 0.45f, c);
        g.DrawLine(pen, c, c - r * 0.45f, c, c + r * 0.45f);
        g.DrawLine(pen, c + r * 0.7f, c + r * 0.7f, s - s * 0.15f, s - s * 0.15f);
        return bmp;
    }

    public static Image ZoomOut(int s)
    {
        var bmp = NewBmp(s);
        using var g = Gfx(bmp);
        using var pen = Pen();
        var c = s / 2f;
        var r = s * 0.22f;
        g.DrawEllipse(pen, c - r, c - r, 2 * r, 2 * r);
        g.DrawLine(pen, c - r * 0.45f, c, c + r * 0.45f, c);
        g.DrawLine(pen, c + r * 0.7f, c + r * 0.7f, s - s * 0.15f, s - s * 0.15f);
        return bmp;
    }

    public static Image SettingsGear(int s)
    {
        var bmp = NewBmp(s);
        using var g = Gfx(bmp);
        var cx = s / 2f;
        var cy = s / 2f;
        const int teeth = 10;
        var dt = 2 * MathF.PI / teeth;
        var rIn = s * 0.26f;
        var rOut = s * 0.42f;
        var rHole = s * 0.11f;
        var pts = new PointF[teeth * 4];
        var pi = 0;
        for (var i = 0; i < teeth; i++)
        {
            var va = i * dt - MathF.PI / 2f;
            var vb = (i + 0.32f) * dt - MathF.PI / 2f;
            var vc = (i + 0.68f) * dt - MathF.PI / 2f;
            var vd = (i + 1) * dt - MathF.PI / 2f;
            if (i == 0)
                pts[pi++] = new PointF(cx + rIn * MathF.Cos(va), cy + rIn * MathF.Sin(va));
            pts[pi++] = new PointF(cx + rOut * MathF.Cos(vb), cy + rOut * MathF.Sin(vb));
            pts[pi++] = new PointF(cx + rOut * MathF.Cos(vc), cy + rOut * MathF.Sin(vc));
            pts[pi++] = new PointF(cx + rIn * MathF.Cos(vd), cy + rIn * MathF.Sin(vd));
        }

        using var path = new GraphicsPath();
        path.AddPolygon(pts);
        path.AddEllipse(cx - rHole, cy - rHole, 2 * rHole, 2 * rHole);
        path.FillMode = FillMode.Alternate;
        using var fill = new SolidBrush(Color.FromArgb(230, 40, 40, 40));
        g.FillPath(fill, path);
        using var outline = Pen(0.95f);
        g.DrawPath(outline, path);
        return bmp;
    }

    public static Image Reload(int s)
    {
        var bmp = NewBmp(s);
        using var g = Gfx(bmp);
        using var pen = Pen();
        var c = new PointF(s / 2f, s / 2f);
        var r = s * 0.28f;
        g.DrawArc(pen, c.X - r, c.Y - r, 2 * r, 2 * r, 40, 270);
        var tip = new PointF(c.X + r * 0.55f, c.Y - r * 0.85f);
        ArrowHead(g, pen, tip, -MathF.PI / 4f, s * 0.12f);
        return bmp;
    }

    private static Bitmap NewBmp(int s)
    {
        var bmp = new Bitmap(s, s, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        bmp.SetResolution(96f, 96f);
        return bmp;
    }

    private static Graphics Gfx(Bitmap bmp)
    {
        var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.Clear(Color.Transparent);
        return g;
    }

    private static Pen Pen(float w = 1.6f) =>
        new(Color.FromArgb(230, 45, 45, 45), w) { StartCap = LineCap.Round, EndCap = LineCap.Round };

    private static void Arrow(Graphics g, Pen pen, float x1, float y1, float x2, float y2, float x3, float y3)
    {
        g.DrawLine(pen, x1, y1, x2, y2);
        g.DrawLine(pen, x2, y2, x3, y3);
    }

    private static void ArrowHead(Graphics g, Pen pen, PointF tip, float angle, float len)
    {
        var a1 = angle + MathF.PI * 0.85f;
        var a2 = angle - MathF.PI * 0.85f;
        g.DrawLine(pen, tip, new PointF(tip.X + len * MathF.Cos(a1), tip.Y + len * MathF.Sin(a1)));
        g.DrawLine(pen, tip, new PointF(tip.X + len * MathF.Cos(a2), tip.Y + len * MathF.Sin(a2)));
    }
}
