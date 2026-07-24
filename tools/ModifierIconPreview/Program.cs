using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using CreatureManager;

const int IconSize = ModifierIconSource.Size;

string output = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "modifier-icons-exact-preview.png"));
string? individualIconDirectory = args.Length > 1 ? Path.GetFullPath(args[1]) : null;
IReadOnlyList<ModifierIconSpec> icons = ModifierIconSource.All;

using Bitmap preview = RenderPreview(icons);
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
preview.Save(output, ImageFormat.Png);
Console.WriteLine(output);

if (individualIconDirectory != null)
{
    SaveIndividualIcons(icons, individualIconDirectory);
    Console.WriteLine(individualIconDirectory);
}

static void SaveIndividualIcons(
    IEnumerable<ModifierIconSpec> icons,
    string outputDirectory)
{
    Directory.CreateDirectory(outputDirectory);
    foreach (ModifierIconSpec icon in icons)
    {
        using Bitmap bitmap = CreateIconBitmap(icon);
        bitmap.Save(Path.Combine(outputDirectory, $"{icon.Key}.png"), ImageFormat.Png);
    }
}

static Bitmap RenderPreview(IReadOnlyList<ModifierIconSpec> icons)
{
    const int cols = 5;
    const int sourceZoom = 2;
    const int hudSize = 17;
    const int hudZoom = 4;
    const int cellW = 245;
    const int cellH = 190;
    int rows = (int)Math.Ceiling(icons.Count / (float)cols);
    int width = cols * cellW + 28;
    int height = rows * cellH + 72;

    Bitmap result = new(width, height, PixelFormat.Format32bppArgb);
    using Graphics g = Graphics.FromImage(result);
    g.Clear(Color.FromArgb(18, 22, 27));
    g.SmoothingMode = SmoothingMode.None;
    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

    using Font titleFont = new("Segoe UI", 14, FontStyle.Bold);
    using Font labelFont = new("Segoe UI", 10, FontStyle.Bold);
    using Font smallFont = new("Segoe UI", 8);
    using SolidBrush textBrush = new(Color.Gainsboro);
    using SolidBrush mutedBrush = new(Color.FromArgb(170, 190, 200, 210));
    using Pen borderPen = new(Color.FromArgb(85, 105, 115, 125));

    g.DrawString(
        $"CreatureManager modifier sprites - exact 64x64 pixel masks + {hudSize}px HUD sample",
        titleFont,
        textBrush,
        18,
        15);

    for (int i = 0; i < icons.Count; i++)
    {
        ModifierIconSpec icon = icons[i];
        int col = i % cols;
        int row = i / cols;
        int cellX = 14 + col * cellW;
        int cellY = 54 + row * cellH;
        g.DrawRectangle(borderPen, cellX, cellY, cellW - 14, cellH - 14);

        using Bitmap source = CreateIconBitmap(icon);
        using Bitmap hud = CreateHudSample(source, hudSize);

        Rectangle sourceRect = new(
            cellX + 12,
            cellY + 12,
            IconSize * sourceZoom,
            IconSize * sourceZoom);
        FillChecker(g, sourceRect);
        DrawScaled(g, source, sourceRect, InterpolationMode.NearestNeighbor);

        Rectangle hudRect = new(
            cellX + 160,
            cellY + 29,
            hudSize * hudZoom,
            hudSize * hudZoom);
        FillChecker(g, hudRect);
        DrawScaled(g, hud, hudRect, InterpolationMode.NearestNeighbor);

        g.DrawString(icon.Key, labelFont, textBrush, cellX + 12, cellY + 145);
        g.DrawString("64px source", smallFont, mutedBrush, cellX + 12, cellY + 163);
        g.DrawString($"{hudSize}px HUD", smallFont, mutedBrush, cellX + 160, cellY + 106);
    }

    return result;
}

static Bitmap CreateIconBitmap(ModifierIconSpec icon)
{
    Bitmap bitmap = new(IconSize, IconSize, PixelFormat.Format32bppArgb);
    for (int y = 0; y < IconSize; y++)
    {
        for (int x = 0; x < IconSize; x++)
        {
            ModifierIconTone tone = icon.GetTone(x, y);
            Color color = tone == ModifierIconTone.Clear
                ? Color.Transparent
                : ToDrawingColor(icon.GetColor(tone));
            bitmap.SetPixel(x, IconSize - 1 - y, color);
        }
    }

    return bitmap;
}

static Color ToDrawingColor(ModifierIconColor color)
{
    return Color.FromArgb(255, color.Red, color.Green, color.Blue);
}

static Bitmap CreateHudSample(Bitmap source, int size)
{
    Bitmap hud = new(size, size, PixelFormat.Format32bppArgb);
    using Graphics g = Graphics.FromImage(hud);
    g.Clear(Color.Transparent);
    g.CompositingMode = CompositingMode.SourceCopy;
    DrawScaled(
        g,
        source,
        new Rectangle(0, 0, size, size),
        InterpolationMode.Bilinear);
    return hud;
}

static void DrawScaled(
    Graphics g,
    Bitmap source,
    Rectangle destination,
    InterpolationMode interpolation)
{
    InterpolationMode oldInterpolation = g.InterpolationMode;
    PixelOffsetMode oldPixelOffset = g.PixelOffsetMode;
    g.InterpolationMode = interpolation;
    g.PixelOffsetMode = PixelOffsetMode.Half;
    g.DrawImage(source, destination);
    g.InterpolationMode = oldInterpolation;
    g.PixelOffsetMode = oldPixelOffset;
}

static void FillChecker(Graphics g, Rectangle rect)
{
    using SolidBrush dark = new(Color.FromArgb(34, 38, 44));
    using SolidBrush light = new(Color.FromArgb(45, 50, 58));
    const int block = 8;
    g.FillRectangle(dark, rect);
    for (int y = rect.Top; y < rect.Bottom; y += block)
    {
        for (int x = rect.Left; x < rect.Right; x += block)
        {
            if (((x / block) + (y / block)) % 2 == 0)
            {
                g.FillRectangle(
                    light,
                    x,
                    y,
                    Math.Min(block, rect.Right - x),
                    Math.Min(block, rect.Bottom - y));
            }
        }
    }
}
