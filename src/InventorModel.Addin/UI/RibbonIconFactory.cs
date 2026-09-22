using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace InventorModel.Addin;

internal enum RibbonIconKind
{
    Model,
    Views,
    Chat,
    Settings
}

internal static class RibbonIconFactory
{
    private const int Supersample = 4;

    public static object Create(RibbonIconKind kind, int size)
    {
        int target = Math.Max(16, size);
        using (Bitmap highResolution = DrawIcon(kind, target * Supersample))
        {
            var bitmap = new Bitmap(target, target, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.Clear(Color.Transparent);
                graphics.DrawImage(highResolution, new Rectangle(0, 0, target, target));
            }

            return PictureHost.ToPictureDisp(bitmap);
        }
    }

    private static Bitmap DrawIcon(RibbonIconKind kind, int size)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(Color.Transparent);

            float scale = size / 32f;
            RectangleF tile = new RectangleF(1.5f * scale, 1.5f * scale, 29f * scale, 29f * scale);
            using (GraphicsPath tilePath = RoundedRectangle(tile, 6f * scale))
            using (var fill = new LinearGradientBrush(
                tile,
                Color.FromArgb(25, 106, 219),
                Color.FromArgb(28, 79, 181),
                LinearGradientMode.Vertical))
            using (var border = new Pen(Color.FromArgb(82, 255, 255, 255), Math.Max(0.8f, 0.8f * scale)))
            {
                graphics.FillPath(fill, tilePath);
                graphics.DrawPath(border, tilePath);
            }

            switch (kind)
            {
                case RibbonIconKind.Views:
                    DrawViews(graphics, scale);
                    break;
                case RibbonIconKind.Chat:
                    DrawChat(graphics, scale);
                    break;
                case RibbonIconKind.Settings:
                    DrawSettings(graphics, scale);
                    break;
                default:
                    DrawModel(graphics, scale);
                    break;
            }
        }

        return bitmap;
    }

    private static void DrawModel(Graphics graphics, float scale)
    {
        PointF top = P(15.2f, 7.1f, scale);
        PointF leftTop = P(8.2f, 11.0f, scale);
        PointF center = P(15.2f, 15.0f, scale);
        PointF rightTop = P(22.2f, 11.0f, scale);
        PointF leftBottom = P(8.2f, 19.0f, scale);
        PointF bottom = P(15.2f, 23.1f, scale);
        PointF rightBottom = P(22.2f, 19.0f, scale);

        using (var face = new SolidBrush(Color.FromArgb(30, 255, 255, 255)))
        {
            graphics.FillPolygon(face, new[] { top, leftTop, center, rightTop });
            graphics.FillPolygon(face, new[] { leftTop, center, bottom, leftBottom });
            graphics.FillPolygon(face, new[] { center, rightTop, rightBottom, bottom });
        }

        using (Pen pen = WhitePen(scale))
        {
            graphics.DrawLine(pen, top, leftTop);
            graphics.DrawLine(pen, top, rightTop);
            graphics.DrawLine(pen, leftTop, center);
            graphics.DrawLine(pen, rightTop, center);
            graphics.DrawLine(pen, leftTop, leftBottom);
            graphics.DrawLine(pen, center, bottom);
            graphics.DrawLine(pen, rightTop, rightBottom);
            graphics.DrawLine(pen, leftBottom, bottom);
            graphics.DrawLine(pen, rightBottom, bottom);
        }
    }

    private static void DrawViews(Graphics graphics, float scale)
    {
        using (Pen pen = WhitePen(scale))
        {
            DrawRect(graphics, pen, 7.0f, 7.5f, 7.2f, 7.2f, scale);
            DrawRect(graphics, pen, 17.8f, 7.5f, 7.2f, 7.2f, scale);
            DrawRect(graphics, pen, 7.0f, 18.0f, 7.2f, 7.2f, scale);
            DrawRect(graphics, pen, 17.8f, 18.0f, 7.2f, 7.2f, scale);
        }
    }

    private static void DrawChat(Graphics graphics, float scale)
    {
        RectangleF bubble = new RectangleF(6.5f * scale, 7.0f * scale, 19f * scale, 14f * scale);
        using (GraphicsPath path = RoundedRectangle(bubble, 4f * scale))
        using (Pen pen = WhitePen(scale))
        {
            graphics.DrawPath(pen, path);
            PointF a = P(12.0f, 21.0f, scale);
            PointF b = P(10.1f, 25.0f, scale);
            PointF c = P(16.1f, 21.0f, scale);
            graphics.DrawLines(pen, new[] { a, b, c });
            graphics.DrawLine(pen, P(10.5f, 12.0f, scale), P(21.5f, 12.0f, scale));
            graphics.DrawLine(pen, P(10.5f, 16.0f, scale), P(18.3f, 16.0f, scale));
        }
    }

    private static void DrawSettings(Graphics graphics, float scale)
    {
        using (Pen pen = WhitePen(scale))
        {
            graphics.DrawLine(pen, P(7.0f, 10.0f, scale), P(25.0f, 10.0f, scale));
            graphics.DrawLine(pen, P(7.0f, 16.0f, scale), P(25.0f, 16.0f, scale));
            graphics.DrawLine(pen, P(7.0f, 22.0f, scale), P(25.0f, 22.0f, scale));
            DrawKnob(graphics, 12.0f, 10.0f, scale);
            DrawKnob(graphics, 20.0f, 16.0f, scale);
            DrawKnob(graphics, 15.0f, 22.0f, scale);
        }
    }

    private static void DrawKnob(Graphics graphics, float x, float y, float scale)
    {
        float radius = 2.0f * scale;
        using (var fill = new SolidBrush(Color.White))
            graphics.FillEllipse(fill, x * scale - radius, y * scale - radius, radius * 2, radius * 2);
    }

    private static void DrawRect(Graphics graphics, Pen pen, float x, float y, float width, float height, float scale)
        => graphics.DrawRectangle(pen, x * scale, y * scale, width * scale, height * scale);

    private static Pen WhitePen(float scale) =>
        new Pen(Color.White, Math.Max(1.05f * scale, 1.15f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

    private static PointF P(float x, float y, float scale) => new PointF(x * scale, y * scale);

    private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        float diameter = Math.Max(1f, radius * 2f);
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private sealed class PictureHost : AxHost
    {
        private PictureHost() : base(string.Empty) { }
        public static object ToPictureDisp(Image image) => GetIPictureDispFromPicture(image);
    }
}
