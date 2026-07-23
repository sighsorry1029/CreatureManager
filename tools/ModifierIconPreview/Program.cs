using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

const int IconSize = 64;

string output = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "modifier-icons-exact-preview.png"));
string? individualIconDirectory = args.Length > 1 ? Path.GetFullPath(args[1]) : null;

IconSpec[] icons =
{
    // Offense
    new("enraged", IsSwordPixel, FromUnity(1f, 0.23f, 0.12f)),
    new("fire", IsFlamePixel, FromUnity(1f, 0.2f, 0.02f), FromUnity(1f, 0.82f, 0.08f)),
    new("frost", IsSnowPixel, FromUnity(0.35f, 0.85f, 1f)),
    new("lightning", IsBoltPixel, FromUnity(1f, 0.9f, 0.08f)),
    new(
        "spirit",
        IsSpiritPixel,
        FromUnity(1f, 0.68f, 0.05f),
        FromUnity(0.85f, 0.15f, 0.08f),
        ToneShape: GetSpiritTone,
        SecondaryColor: FromUnity(1f, 0.95f, 0.6f)),
    new(
        "armorPiercing",
        IsArmorPiercingPixel,
        FromUnity(0.48f, 0.75f, 1f),
        FromUnity(1f, 0.52f, 0.08f),
        ToneShape: GetArmorPiercingTone,
        SecondaryColor: FromUnity(0.2f, 0.25f, 0.55f)),
    new(
        "staggering",
        IsStaggeringPixel,
        FromUnity(1f, 0.55f, 0.02f),
        FromUnity(1f, 0.95f, 0.28f),
        ToneShape: GetStaggeringTone,
        SecondaryColor: FromUnity(1f, 0.86f, 0.12f)),
    new("undodgeable", IsUndodgeablePixel, FromUnity(1f, 0.96f, 0.9f), FromUnity(1f, 0.12f, 0.08f)),

    // Defense
    new("armored", IsCuirassPixel, FromUnity(0.08f, 0.28f, 0.68f)),
    new(
        "deathward",
        IsDeathwardPixel,
        FromUnity(0.36f, 0.12f, 0.62f),
        FromUnity(0.18f, 0.04f, 0.32f),
        ToneShape: GetDeathwardTone,
        SecondaryColor: FromUnity(0.94f, 0.85f, 1f)),
    new("regenerating", IsCrossPixel, FromUnity(0.2f, 0.95f, 0.25f)),
    new(
        "reflection",
        IsReflectionPixel,
        FromUnity(0.56f, 0.84f, 0.92f),
        FromUnity(0.96f, 1f, 1f),
        ToneShape: GetReflectionTone,
        SecondaryColor: FromUnity(0.32f, 0.68f, 1f)),
    new("vortex", IsSpiralPixel, FromUnity(0.58f, 0.72f, 0.82f), FromUnity(0.36f, 0.55f, 0.68f)),
    new("adaptive", IsAdaptivePixel, FromUnity(0.7f, 1f, 0.28f), FromUnity(0.92f, 1f, 0.58f)),
    new("unflinching", IsUnflinchingPixel, FromUnity(0.92f, 0.82f, 0.55f), FromUnity(1f, 0.62f, 0.11f)),
    new("chameleon", IsChameleonPixel, FromUnity(0.1f, 0.78f, 0.62f), FromUnity(0.82f, 1f, 0.18f)),

    // Affliction
    new("exposed", IsBrokenShieldPixel, FromUnity(0.42f, 0.82f, 0.98f), FromUnity(0.72f, 0.96f, 1f)),
    new(
        "weakened",
        IsWeakenedPixel,
        FromUnity(0.82f, 0.84f, 0.85f),
        FromUnity(0.96f, 0.68f, 0.24f),
        ToneShape: GetWeakenedTone,
        SecondaryColor: FromUnity(0.32f, 0.11f, 0.15f)),
    new("withered", IsWiltedLeafPixel, FromUnity(0.66f, 0.78f, 0.22f)),
    new("crippling", IsSnarePixel, FromUnity(0.78f, 0.45f, 1f)),
    new("disruptive", IsInterferencePixel, FromUnity(0.14f, 1f, 0.86f)),
    new("adrenalineDrain", IsAdrenalineDrainPixel, FromUnity(1f, 0.35f, 0.42f)),
    new(
        "corrosive",
        IsCorrosivePixel,
        FromUnity(0.72f, 0.78f, 0.81f),
        FromUnity(0.95f, 0.32f, 0.06f),
        ToneShape: GetCorrosiveTone,
        SecondaryColor: FromUnity(0.34f, 0.43f, 0.48f)),
    new("toxicDeath", IsSkullPixel, FromUnity(0.45f, 1f, 0.18f)),

    // Special
    new("swift", IsChevronPixel, FromUnity(0.25f, 0.95f, 0.55f)),
    new("attackSpeed", IsHourglassPixel, FromUnity(1f, 0.62f, 0.08f)),
    new(
        "vampiric",
        IsVampiricPixel,
        FromUnity(0.96f, 0.97f, 1f),
        FromUnity(0.98f, 0.03f, 0.07f),
        ToneShape: GetVampiricTone,
        SecondaryColor: FromUnity(0.92f, 0.94f, 1f)),
    new(
        "reaping",
        IsReapingPixel,
        FromUnity(0.36f, 0.08f, 0.16f),
        FromUnity(0.82f, 0.88f, 0.93f),
        ToneShape: GetReapingTone,
        SecondaryColor: FromUnity(0.48f, 0.28f, 0.13f)),
    new("blink", IsBlinkPixel, FromUnity(0.58f, 0.36f, 1f)),
    new("omen", IsEyePixel, FromUnity(0.78f, 0.49f, 1f), FromUnity(1f, 0.188f, 0.157f)),
    new("juggernaut", IsAnchorPixel, FromUnity(0.82f, 0.72f, 0.48f)),
    new(
        "blamer",
        IsBlamerPixel,
        FromUnity(0.78f, 0.52f, 0.15f),
        FromUnity(1f, 0.78f, 0.22f),
        ToneShape: GetBlamerTone,
        SecondaryColor: FromUnity(0.34f, 0.22f, 0.07f))
};

using Bitmap preview = RenderPreview(icons);
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
preview.Save(output, ImageFormat.Png);
Console.WriteLine(output);

if (individualIconDirectory != null)
{
    SaveIndividualIcons(icons, individualIconDirectory);
    Console.WriteLine(individualIconDirectory);
}

static void SaveIndividualIcons(IEnumerable<IconSpec> icons, string outputDirectory)
{
    Directory.CreateDirectory(outputDirectory);
    foreach (IconSpec icon in icons)
    {
        using Bitmap bitmap = CreateIconBitmap(icon);
        bitmap.Save(Path.Combine(outputDirectory, $"{icon.Name}.png"), ImageFormat.Png);
    }
}

static Bitmap RenderPreview(IReadOnlyList<IconSpec> icons)
{
    const int cols = 5;
    const int sourceZoom = 2;
    const int hudSize = 16;
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

    g.DrawString($"CreatureManager modifier sprites - exact 64x64 pixel masks + {hudSize}px HUD sample", titleFont, textBrush, 18, 15);

    for (int i = 0; i < icons.Count; i++)
    {
        IconSpec icon = icons[i];
        int col = i % cols;
        int row = i / cols;
        int cellX = 14 + col * cellW;
        int cellY = 54 + row * cellH;
        g.DrawRectangle(borderPen, cellX, cellY, cellW - 14, cellH - 14);

        using Bitmap source = CreateIconBitmap(icon);
        using Bitmap hud = CreateHudSample(source, hudSize);

        Rectangle sourceRect = new(cellX + 12, cellY + 12, IconSize * sourceZoom, IconSize * sourceZoom);
        FillChecker(g, sourceRect);
        DrawScaled(g, source, sourceRect, InterpolationMode.NearestNeighbor);

        Rectangle hudRect = new(cellX + 160, cellY + 29, hudSize * hudZoom, hudSize * hudZoom);
        FillChecker(g, hudRect);
        DrawScaled(g, hud, hudRect, InterpolationMode.NearestNeighbor);

        g.DrawString(icon.Name, labelFont, textBrush, cellX + 12, cellY + 145);
        g.DrawString("64px source", smallFont, mutedBrush, cellX + 12, cellY + 163);
        g.DrawString($"{hudSize}px HUD", smallFont, mutedBrush, cellX + 160, cellY + 106);
    }

    return result;
}

static Bitmap CreateIconBitmap(IconSpec icon)
{
    Bitmap bitmap = new(IconSize, IconSize, PixelFormat.Format32bppArgb);
    for (int y = 0; y < IconSize; y++)
    {
        for (int x = 0; x < IconSize; x++)
        {
            bool active;
            Color color;
            if (icon.ToneShape != null)
            {
                IconTone tone = icon.ToneShape(x, y);
                active = tone != IconTone.Clear;
                color = tone switch
                {
                    IconTone.Secondary when icon.SecondaryColor.HasValue => icon.SecondaryColor.Value,
                    IconTone.Accent when icon.AccentColor.HasValue => icon.AccentColor.Value,
                    _ => icon.Color
                };
            }
            else
            {
                active = icon.Shape(x, y, out bool useAccent);
                color = useAccent && icon.AccentColor.HasValue ? icon.AccentColor.Value : icon.Color;
            }

            bitmap.SetPixel(x, IconSize - 1 - y, active ? color : Color.Transparent);
        }
    }

    return bitmap;
}

static Bitmap CreateHudSample(Bitmap source, int size)
{
    Bitmap hud = new(size, size, PixelFormat.Format32bppArgb);
    using Graphics g = Graphics.FromImage(hud);
    g.Clear(Color.Transparent);
    g.CompositingMode = CompositingMode.SourceCopy;
    DrawScaled(g, source, new Rectangle(0, 0, size, size), InterpolationMode.Bilinear);
    return hud;
}

static void DrawScaled(Graphics g, Bitmap source, Rectangle destination, InterpolationMode interpolation)
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
                g.FillRectangle(light, x, y, Math.Min(block, rect.Right - x), Math.Min(block, rect.Bottom - y));
            }
        }
    }
}

static Color FromUnity(float r, float g, float b) => Color.FromArgb(255, Channel(r), Channel(g), Channel(b));

static int Channel(float value) => Math.Clamp((int)MathF.Round(value * 255f), 0, 255);

static bool IsCuirassPixel(int x, int y, out bool isAccent)
{
    isAccent = false;
    Vec2 point = new(x + 0.5f, y + 0.5f);
    bool outer = IsPointInPolygon(point, new[]
    {
        new Vec2(10f, 58f),
        new Vec2(23f, 61f),
        new Vec2(41f, 61f),
        new Vec2(54f, 58f),
        new Vec2(52f, 48f),
        new Vec2(57f, 39f),
        new Vec2(52f, 32f),
        new Vec2(49f, 11f),
        new Vec2(32f, 5f),
        new Vec2(15f, 11f),
        new Vec2(12f, 32f),
        new Vec2(7f, 39f),
        new Vec2(12f, 48f)
    });
    bool neck = Distance(point, new Vec2(32f, 62f)) <= 10.5f;
    bool leftArmhole = Distance(point, new Vec2(4f, 49f)) <= 11.5f;
    bool rightArmhole = Distance(point, new Vec2(60f, 49f)) <= 11.5f;
    return outer && !neck && !leftArmhole && !rightArmhole;
}

static bool IsSwordPixel(int x, int y, out bool isBorder)
{
    isBorder = false;
    Vec2 point = new(x + 0.5f, y + 0.5f);
    if (IsSwordBladePixel(point, out isBorder))
    {
        return true;
    }

    if (IsSwordGuardPixel(point, out isBorder))
    {
        return true;
    }

    return IsSwordGripPixel(point, out isBorder);
}

static bool IsDeathwardPixel(int x, int y, out bool isBorder)
{
    isBorder = false;
    return GetDeathwardTone(x, y) != IconTone.Clear;
}

static IconTone GetDeathwardTone(int x, int y)
{
    Vec2 point = new(x + 0.5f, y + 0.5f);
    bool body = point.X >= 15f && point.X <= 49f && point.Y >= 14f && point.Y <= 43f;
    bool arch = Distance(point, new Vec2(32f, 43f)) <= 17f && point.Y >= 43f;
    bool tombstone = body || arch;
    bool cross = tombstone &&
                 (MathF.Abs(point.X - 32f) <= 3.5f && point.Y >= 23f && point.Y <= 48f ||
                  MathF.Abs(point.Y - 37f) <= 3.5f && point.X >= 23f && point.X <= 41f);
    bool baseStone = point.X >= 10f && point.X <= 54f && point.Y >= 7f && point.Y <= 14f;

    if (cross)
    {
        return IconTone.Secondary;
    }

    if (baseStone)
    {
        return IconTone.Accent;
    }

    return tombstone ? IconTone.Primary : IconTone.Clear;
}

static bool IsChevronPixel(int x, int y, out bool isBorder)
{
    isBorder = false;
    Vec2 point = new(x + 0.5f, y + 0.5f);
    bool first = IsThickLine(point, new Vec2(10f, 16f), new Vec2(31f, 32f), 5f) ||
                 IsThickLine(point, new Vec2(31f, 32f), new Vec2(10f, 48f), 5f);
    bool second = IsThickLine(point, new Vec2(28f, 16f), new Vec2(49f, 32f), 5f) ||
                  IsThickLine(point, new Vec2(49f, 32f), new Vec2(28f, 48f), 5f);
    return first || second;
}

static bool IsCrossPixel(int x, int y, out bool isBorder)
{
    isBorder = false;
    return (x >= 25 && x <= 38 && y >= 9 && y <= 55) ||
           (x >= 9 && x <= 55 && y >= 25 && y <= 38);
}

static bool IsVampiricPixel(int x, int y, out bool isBorder)
{
    isBorder = false;
    return GetVampiricTone(x, y) != IconTone.Clear;
}

static IconTone GetVampiricTone(int x, int y)
{
    Vec2 point = new(x + 0.5f, y + 0.5f);
    bool outerFangs = IsPointInPolygon(point, new[]
    {
        new Vec2(7f, 49f),
        new Vec2(20f, 49f),
        new Vec2(22f, 46f),
        new Vec2(21f, 34f),
        new Vec2(18f, 12f),
        new Vec2(14f, 21f),
        new Vec2(9f, 36f)
    }) || IsPointInPolygon(point, new[]
    {
        new Vec2(44f, 49f),
        new Vec2(57f, 49f),
        new Vec2(55f, 36f),
        new Vec2(50f, 12f),
        new Vec2(46f, 21f),
        new Vec2(43f, 34f),
        new Vec2(42f, 46f)
    });
    bool innerTeeth = IsPointInPolygon(point, new[]
    {
        new Vec2(23f, 49f),
        new Vec2(31f, 49f),
        new Vec2(31f, 31f),
        new Vec2(29f, 27f),
        new Vec2(25f, 27f),
        new Vec2(23f, 31f)
    }) || IsPointInPolygon(point, new[]
    {
        new Vec2(33f, 49f),
        new Vec2(41f, 49f),
        new Vec2(41f, 31f),
        new Vec2(39f, 27f),
        new Vec2(35f, 27f),
        new Vec2(33f, 31f)
    });
    bool drop = IsPointInPolygon(point, new[]
    {
        new Vec2(32f, 23f),
        new Vec2(27f, 14f),
        new Vec2(28f, 8f),
        new Vec2(32f, 5f),
        new Vec2(36f, 8f),
        new Vec2(37f, 14f)
    });

    if (drop)
    {
        return IconTone.Accent;
    }

    if (innerTeeth)
    {
        return IconTone.Secondary;
    }

    return outerFangs ? IconTone.Primary : IconTone.Clear;
}

static bool IsFlamePixel(int x, int y, out bool isBorder)
{
    Vec2 point = new(x + 0.5f, y + 0.5f);
    Vec2[] flame =
    {
        new(34f, 61f),
        new(40f, 52f),
        new(43f, 44f),
        new(49f, 35f),
        new(53f, 26f),
        new(51f, 16f),
        new(45f, 8f),
        new(36f, 4f),
        new(27f, 5f),
        new(18f, 10f),
        new(12f, 18f),
        new(9f, 28f),
        new(11f, 39f),
        new(17f, 49f),
        new(25f, 56f)
    };
    bool outer = IsPointInPolygon(point, flame);
    bool inner = IsPointInPolygon(point, new[]
    {
        new Vec2(32f, 40f),
        new Vec2(38f, 32f),
        new Vec2(41f, 24f),
        new Vec2(39f, 16f),
        new Vec2(33f, 11f),
        new Vec2(27f, 14f),
        new Vec2(23f, 21f),
        new Vec2(24f, 29f)
    });
    isBorder = outer && inner;
    return outer;
}

static bool IsSnowPixel(int x, int y, out bool isBorder)
{
    isBorder = false;
    Vec2 point = new(x + 0.5f, y + 0.5f);
    return IsThickLine(point, new Vec2(10f, 32f), new Vec2(54f, 32f), 2.5f) ||
           IsThickLine(point, new Vec2(32f, 10f), new Vec2(32f, 54f), 2.5f) ||
           IsThickLine(point, new Vec2(16f, 16f), new Vec2(48f, 48f), 2.5f) ||
           IsThickLine(point, new Vec2(16f, 48f), new Vec2(48f, 16f), 2.5f);
}

static bool IsBoltPixel(int x, int y, out bool isBorder)
{
    isBorder = false;
    Vec2 point = new(x + 0.5f, y + 0.5f);
    Vec2[] polygon =
    {
        new(36f, 58f),
        new(16f, 29f),
        new(29f, 29f),
        new(24f, 6f),
        new(49f, 38f),
        new(35f, 38f)
    };
    return IsPointInPolygon(point, polygon);
}

static bool IsSkullPixel(int x, int y, out bool isBorder)
{
    isBorder = false;
    Vec2 point = new(x + 0.5f, y + 0.5f);
    bool head = Distance(point, new Vec2(31.5f, 36f)) <= 20f;
    bool jaw = x >= 21 && x <= 42 && y >= 10 && y <= 28;
    bool eyeLeft = Distance(point, new Vec2(24f, 39f)) <= 4f;
    bool eyeRight = Distance(point, new Vec2(39f, 39f)) <= 4f;
    bool nose = MathF.Abs(point.X - 31.5f) + MathF.Abs(point.Y - 28f) <= 5f;
    return (head || jaw) && !eyeLeft && !eyeRight && !nose;
}

static bool IsStaggeringPixel(int x, int y, out bool isBorder)
{
    isBorder = false;
    return GetStaggeringTone(x, y) != IconTone.Clear;
}

static IconTone GetStaggeringTone(int x, int y)
{
    Vec2 point = new(x + 0.5f, y + 0.5f);
    const float radians = 20f * MathF.PI / 180f;
    float dx = point.X - 32f;
    float dy = point.Y - 31f;
    float orbitX = dx * MathF.Cos(radians) + dy * MathF.Sin(radians);
    float orbitY = -dx * MathF.Sin(radians) + dy * MathF.Cos(radians);
    float outer = orbitX * orbitX / (29f * 29f) + orbitY * orbitY / (17f * 17f);
    float inner = orbitX * orbitX / (23f * 23f) + orbitY * orbitY / (11f * 11f);
    bool orbit = outer <= 1f && inner >= 1f;
    bool centerStar = IsPointInPolygon(point, new[]
    {
        new Vec2(30f, 57f),
        new Vec2(35f, 41f),
        new Vec2(51f, 36f),
        new Vec2(35f, 31f),
        new Vec2(30f, 15f),
        new Vec2(25f, 31f),
        new Vec2(10f, 36f),
        new Vec2(25f, 41f)
    });
    bool smallStar = IsPointInPolygon(point, new[]
    {
        new Vec2(14f, 61f),
        new Vec2(17f, 54f),
        new Vec2(24f, 51f),
        new Vec2(17f, 48f),
        new Vec2(14f, 41f),
        new Vec2(11f, 48f),
        new Vec2(4f, 51f),
        new Vec2(11f, 54f)
    });

    if (centerStar)
    {
        return IconTone.Accent;
    }

    if (smallStar)
    {
        return IconTone.Secondary;
    }

    return orbit ? IconTone.Primary : IconTone.Clear;
}

static bool IsHourglassPixel(int x, int y, out bool isBorder)
{
    isBorder = false;
    Vec2 point = new(x + 0.5f, y + 0.5f);
    bool top = IsThickLine(point, new Vec2(18f, 52f), new Vec2(46f, 52f), 3f);
    bool bottom = IsThickLine(point, new Vec2(18f, 12f), new Vec2(46f, 12f), 3f);
    bool left = IsThickLine(point, new Vec2(20f, 49f), new Vec2(31.5f, 32f), 3.5f) ||
                IsThickLine(point, new Vec2(31.5f, 32f), new Vec2(20f, 15f), 3.5f);
    bool right = IsThickLine(point, new Vec2(44f, 49f), new Vec2(31.5f, 32f), 3.5f) ||
                 IsThickLine(point, new Vec2(31.5f, 32f), new Vec2(44f, 15f), 3.5f);
    bool sand = MathF.Abs(point.X - 31.5f) <= 5f && point.Y >= 27f && point.Y <= 37f;
    return top || bottom || left || right || sand;
}

static bool IsSpiralPixel(int x, int y, out bool isBorder)
{
    Vec2 point = new(x + 0.5f, y + 0.5f);
    Vec2 center = new(31.5f, 31.5f);
    Vec2 delta = point - center;
    float radius = delta.Magnitude;
    const float innerRadius = 5f;
    const float outerRadius = 28f;
    if (radius < innerRadius || radius > outerRadius)
    {
        isBorder = false;
        return false;
    }

    float angle = -MathF.Atan2(delta.Y, delta.X);
    float radialProgress = Clamp01((radius - innerRadius) / (outerRadius - innerRadius));
    const float sectorAngle = MathF.PI * 2f / 5f;
    float bladeCenter = Lerp(0.08f, 0.83f, radialProgress);
    float bladeOffset = Repeat(angle - bladeCenter + sectorAngle * 0.5f, sectorAngle) - sectorAngle * 0.5f;
    float halfWidth = 0.035f + 0.38f * MathF.Pow(MathF.Sin(MathF.PI * radialProgress), 0.7f);
    bool blade = MathF.Abs(bladeOffset) <= halfWidth;
    isBorder = blade && bladeOffset < -halfWidth * 0.42f;
    return blade;
}

static bool IsSnarePixel(int x, int y, out bool isBorder)
{
    isBorder = false;
    Vec2 point = new(x + 0.5f, y + 0.5f);
    bool shackle = Distance(point, new Vec2(31.5f, 38f)) <= 17f &&
                   Distance(point, new Vec2(31.5f, 38f)) >= 10f &&
                   point.Y >= 28f;
    bool chain = IsThickLine(point, new Vec2(20f, 24f), new Vec2(44f, 14f), 3f) ||
                 IsThickLine(point, new Vec2(26f, 17f), new Vec2(38f, 28f), 3f);
    bool foot = IsThickLine(point, new Vec2(19f, 10f), new Vec2(49f, 10f), 3f);
    return shackle || chain || foot;
}

static bool IsAdaptivePixel(int x, int y, out bool isCore)
{
    Vec2 point = new(x + 0.5f, y + 0.5f);
    Vec2 center = new(31.5f, 31.5f);
    Vec2 delta = point - center;
    float radius = Distance(point, center);
    float angle = MathF.Atan2(delta.Y, delta.X) * 180f / MathF.PI;
    float localAngle = Repeat(angle + 360f, 120f);
    bool arrowBody = radius is >= 19f and <= 24.5f && localAngle is >= 8f and <= 86f;
    bool arrowHead = IsAdaptiveArrowHead(point, center, 88f) ||
                     IsAdaptiveArrowHead(point, center, 208f) ||
                     IsAdaptiveArrowHead(point, center, 328f);

    Vec2[] core =
    {
        new(31.5f, 40f),
        new(39f, 35.5f),
        new(39f, 27.5f),
        new(31.5f, 23f),
        new(24f, 27.5f),
        new(24f, 35.5f)
    };
    isCore = IsPointInPolygon(point, core);
    return arrowBody || arrowHead || isCore;
}

static bool IsAdaptiveArrowHead(Vec2 point, Vec2 center, float angleDegrees)
{
    float radians = angleDegrees * MathF.PI / 180f;
    Vec2 radial = new(MathF.Cos(radians), MathF.Sin(radians));
    Vec2 tangent = new(-radial.Y, radial.X);
    Vec2 relative = point - (center + radial * 21.75f);
    float forward = Dot(relative, tangent);
    if (forward < -3.5f || forward > 8f)
    {
        return false;
    }

    float halfWidth = Lerp(6.5f, 0f, (forward + 3.5f) / 11.5f);
    return MathF.Abs(Dot(relative, radial)) <= halfWidth;
}

static bool IsEyePixel(int x, int y, out bool isPupil)
{
    Vec2 point = new(x + 0.5f, y + 0.5f);
    Vec2 center = new(31.5f, 31.5f);
    float dx = MathF.Abs(point.X - center.X);
    float dy = MathF.Abs(point.Y - center.Y);
    bool eye = dx / 27f + dy / 18f <= 1f;
    bool pupil = Distance(point, center) <= 8.5f;
    bool border = eye && dx / 20f + dy / 11f >= 1f;
    isPupil = pupil;
    return border || pupil;
}

static bool IsReapingPixel(int x, int y, out bool isAccent)
{
    IconTone tone = GetReapingTone(x, y);
    isAccent = tone == IconTone.Accent;
    return tone != IconTone.Clear;
}

static IconTone GetReapingTone(int x, int y)
{
    Vec2 point = new(x + 0.5f, y + 0.5f);
    bool hood = IsPointInPolygon(point, new[]
    {
        new Vec2(32f, 55f),
        new Vec2(23f, 46f),
        new Vec2(21f, 37f),
        new Vec2(26f, 31f),
        new Vec2(38f, 31f),
        new Vec2(43f, 38f),
        new Vec2(40f, 48f)
    });
    bool robe = IsPointInPolygon(point, new[]
    {
        new Vec2(25f, 34f),
        new Vec2(39f, 34f),
        new Vec2(43f, 25f),
        new Vec2(40f, 18f),
        new Vec2(46f, 7f),
        new Vec2(18f, 7f),
        new Vec2(24f, 20f),
        new Vec2(21f, 28f)
    });
    bool faceOpening = Distance(point, new Vec2(32f, 41.5f)) <= 5.2f;
    bool figure = (hood || robe) && !faceOpening;

    bool handle = IsThickLine(point, new Vec2(13f, 49f), new Vec2(22f, 35f), 2.4f) ||
                  IsThickLine(point, new Vec2(22f, 35f), new Vec2(49f, 7f), 2.4f);
    bool blade = IsPointInPolygon(point, new[]
    {
        new Vec2(9f, 49f),
        new Vec2(14f, 59f),
        new Vec2(28f, 62f),
        new Vec2(43f, 60f),
        new Vec2(57f, 52f),
        new Vec2(49f, 53f),
        new Vec2(38f, 55f),
        new Vec2(28f, 55f),
        new Vec2(18f, 52f),
        new Vec2(13f, 44f)
    });

    if (blade)
    {
        return IconTone.Accent;
    }

    if (handle)
    {
        return IconTone.Secondary;
    }

    return figure ? IconTone.Primary : IconTone.Clear;
}

static bool IsSpiritPixel(int x, int y, out bool isBorder)
{
    isBorder = false;
    return GetSpiritTone(x, y) != IconTone.Clear;
}

static IconTone GetSpiritTone(int x, int y)
{
    Vec2 point = new(x + 0.5f, y + 4.5f);
    float loopX = (point.X - 32f) / 10f;
    float loopY = (point.Y - 46f) / 13f;
    float innerX = (point.X - 32f) / 4.5f;
    float innerY = (point.Y - 46f) / 7f;
    bool loop = loopX * loopX + loopY * loopY <= 1f && innerX * innerX + innerY * innerY >= 1f;
    bool stem = MathF.Abs(point.X - 32f) <= 3.5f && point.Y >= 9f && point.Y <= 38f;
    bool cross = MathF.Abs(point.Y - 31f) <= 3.5f && point.X >= 17f && point.X <= 47f;
    bool ankh = loop || stem || cross;
    bool jewel = Distance(point, new Vec2(32f, 31f)) <= 3.2f;

    if (jewel)
    {
        return IconTone.Accent;
    }

    if (ankh)
    {
        return IconTone.Primary;
    }

    return IconTone.Clear;
}

static bool IsBrokenShieldPixel(int x, int y, out bool isRightHalf)
{
    Vec2 point = new(x + 0.5f, y + 0.5f);
    Vec2[] shield =
    {
        new(11f, 53f),
        new(53f, 53f),
        new(53f, 32f),
        new(49f, 20f),
        new(41f, 12f),
        new(31.5f, 6f),
        new(23f, 12f),
        new(15f, 20f),
        new(11f, 32f)
    };
    bool crack = IsThickLine(point, new Vec2(31f, 5f), new Vec2(36f, 14f), 3.2f) ||
                 IsThickLine(point, new Vec2(36f, 14f), new Vec2(28f, 25f), 3.2f) ||
                 IsThickLine(point, new Vec2(28f, 25f), new Vec2(36f, 35f), 3.2f) ||
                 IsThickLine(point, new Vec2(36f, 35f), new Vec2(27f, 45f), 3.2f) ||
                 IsThickLine(point, new Vec2(27f, 45f), new Vec2(34f, 55f), 3.2f);
    isRightHalf = point.X > GetBrokenShieldCrackX(point.Y);
    return IsPointInPolygon(point, shield) && !crack;
}

static float GetBrokenShieldCrackX(float y)
{
    if (y <= 14f) return Lerp(31f, 36f, Clamp01((y - 5f) / 9f));
    if (y <= 25f) return Lerp(36f, 28f, Clamp01((y - 14f) / 11f));
    if (y <= 35f) return Lerp(28f, 36f, Clamp01((y - 25f) / 10f));
    if (y <= 45f) return Lerp(36f, 27f, Clamp01((y - 35f) / 10f));
    return Lerp(27f, 34f, Clamp01((y - 45f) / 10f));
}

static bool IsArmorPiercingPixel(int x, int y, out bool isBorder)
{
    isBorder = false;
    return GetArmorPiercingTone(x, y) != IconTone.Clear;
}

static IconTone GetArmorPiercingTone(int x, int y)
{
    Vec2 point = new(x + 0.5f, y + 0.5f);
    bool shield = IsPointInPolygon(point, new[]
    {
        new Vec2(13f, 53f),
        new Vec2(48f, 56f),
        new Vec2(54f, 48f),
        new Vec2(53f, 31f),
        new Vec2(48f, 19f),
        new Vec2(34f, 7f),
        new Vec2(20f, 13f),
        new Vec2(12f, 26f),
        new Vec2(10f, 42f)
    });
    bool crack = shield && IsPointInPolygon(point, new[]
    {
        new Vec2(29f, 48f),
        new Vec2(35f, 42f),
        new Vec2(33f, 38f),
        new Vec2(40f, 35f),
        new Vec2(36f, 31f),
        new Vec2(43f, 27f),
        new Vec2(38f, 22f),
        new Vec2(34f, 24f),
        new Vec2(29f, 18f),
        new Vec2(26f, 24f),
        new Vec2(22f, 21f),
        new Vec2(23f, 28f),
        new Vec2(18f, 31f),
        new Vec2(24f, 35f),
        new Vec2(21f, 39f),
        new Vec2(27f, 41f),
        new Vec2(26f, 46f)
    });
    bool arrowShaft = IsThickLine(point, new Vec2(30f, 30f), new Vec2(56f, 56f), 4.8f);
    bool arrowHead = IsPointInPolygon(point, new[]
    {
        new Vec2(4f, 4f),
        new Vec2(8f, 22f),
        new Vec2(13f, 17f),
        new Vec2(20f, 20f),
        new Vec2(17f, 13f),
        new Vec2(22f, 8f)
    });

    if (arrowShaft || arrowHead)
    {
        return IconTone.Accent;
    }

    if (crack)
    {
        return IconTone.Secondary;
    }

    if (shield)
    {
        return IconTone.Primary;
    }

    return IconTone.Clear;
}

static bool IsUndodgeablePixel(int x, int y, out bool isMiddleDiamond)
{
    Vec2 point = new(x + 0.5f, y + 0.5f);
    bool corners =
        IsThickLine(point, new Vec2(8f, 54f), new Vec2(23f, 54f), 2.5f) ||
        IsThickLine(point, new Vec2(8f, 54f), new Vec2(8f, 39f), 2.5f) ||
        IsThickLine(point, new Vec2(41f, 54f), new Vec2(56f, 54f), 2.5f) ||
        IsThickLine(point, new Vec2(56f, 54f), new Vec2(56f, 39f), 2.5f) ||
        IsThickLine(point, new Vec2(8f, 10f), new Vec2(23f, 10f), 2.5f) ||
        IsThickLine(point, new Vec2(8f, 10f), new Vec2(8f, 25f), 2.5f) ||
        IsThickLine(point, new Vec2(41f, 10f), new Vec2(56f, 10f), 2.5f) ||
        IsThickLine(point, new Vec2(56f, 10f), new Vec2(56f, 25f), 2.5f);
    bool outer = IsPointInPolygon(point, new[]
    {
        new Vec2(32f, 45f),
        new Vec2(45f, 32f),
        new Vec2(32f, 19f),
        new Vec2(19f, 32f)
    });
    bool inner = IsPointInPolygon(point, new[]
    {
        new Vec2(32f, 38f),
        new Vec2(38f, 32f),
        new Vec2(32f, 26f),
        new Vec2(26f, 32f)
    });
    bool diamond = outer && !inner;
    bool center = Distance(point, new Vec2(32f, 32f)) <= 3.5f;
    isMiddleDiamond = diamond;
    return corners || diamond || center;
}

static bool IsWeakenedPixel(int x, int y, out bool isBorder)
{
    isBorder = false;
    return GetWeakenedTone(x, y) != IconTone.Clear;
}

static IconTone GetWeakenedTone(int x, int y)
{
    Vec2 point = new(x + 0.5f, y + 0.5f);
    bool attachedBlade = IsPointInPolygon(point, new[]
    {
        new Vec2(21f, 36f),
        new Vec2(29f, 39f),
        new Vec2(42f, 19f),
        new Vec2(39f, 14f),
        new Vec2(36f, 16f),
        new Vec2(32f, 12f),
        new Vec2(27f, 20f)
    });
    bool tipBlade = IsPointInPolygon(point, new[]
    {
        new Vec2(44f, 25f),
        new Vec2(53f, 33f),
        new Vec2(61f, 51f),
        new Vec2(48f, 46f),
        new Vec2(41f, 37f),
        new Vec2(47f, 33f)
    });
    bool attachedGoldEdge = IsPointInPolygon(point, new[]
    {
        new Vec2(19f, 36f),
        new Vec2(22f, 35f),
        new Vec2(29f, 18f),
        new Vec2(33f, 11f),
        new Vec2(31f, 9f),
        new Vec2(26f, 17f)
    });
    bool tipGoldEdge = IsPointInPolygon(point, new[]
    {
        new Vec2(43f, 24f),
        new Vec2(53f, 32f),
        new Vec2(62f, 51f),
        new Vec2(59f, 46f),
        new Vec2(51f, 34f),
        new Vec2(46f, 27f)
    });
    bool grip = IsThickLine(point, new Vec2(14f, 53f), new Vec2(23f, 42f), 4.2f);
    bool guard = IsPointInPolygon(point, new[]
    {
        new Vec2(7f, 34f),
        new Vec2(14f, 35f),
        new Vec2(22f, 39f),
        new Vec2(31f, 47f),
        new Vec2(32f, 51f),
        new Vec2(27f, 47f),
        new Vec2(20f, 42f),
        new Vec2(12f, 38f)
    });
    bool pommel = Distance(point, new Vec2(11f, 57f)) <= 5f;

    if (grip || guard)
    {
        return IconTone.Secondary;
    }

    if (pommel || attachedGoldEdge || tipGoldEdge)
    {
        return IconTone.Accent;
    }

    return attachedBlade || tipBlade ? IconTone.Primary : IconTone.Clear;
}

static bool IsWiltedLeafPixel(int x, int y, out bool isBorder)
{
    Vec2 point = new(x + 0.5f, y + 0.5f);
    bool stem = IsThickLine(point, new Vec2(27f, 8f), new Vec2(38f, 56f), 2.6f);
    bool leafLeft = IsPointInPolygon(point, new[]
    {
        new Vec2(31f, 33f),
        new Vec2(9f, 48f),
        new Vec2(26f, 55f)
    });
    bool leafRight = IsPointInPolygon(point, new[]
    {
        new Vec2(34f, 27f),
        new Vec2(55f, 40f),
        new Vec2(39f, 49f)
    });
    bool droop = IsThickLine(point, new Vec2(35f, 20f), new Vec2(50f, 12f), 3f);
    isBorder = stem || droop;
    return stem || leafLeft || leafRight || droop;
}

static bool IsReflectionPixel(int x, int y, out bool isAccent)
{
    IconTone tone = GetReflectionTone(x, y);
    isAccent = tone == IconTone.Accent;
    return tone != IconTone.Clear;
}

static IconTone GetReflectionTone(int x, int y)
{
    Vec2 point = new(x + 0.5f, y + 0.5f);
    bool shield = IsPointInPolygon(point, new[]
    {
        new Vec2(34f, 55f),
        new Vec2(54f, 50f),
        new Vec2(54f, 32f),
        new Vec2(50f, 20f),
        new Vec2(41f, 11f),
        new Vec2(33f, 16f),
        new Vec2(28f, 26f),
        new Vec2(27f, 40f)
    });
    bool reflectedPath = IsThickLine(point, new Vec2(7f, 54f), new Vec2(34f, 34f), 3.4f) ||
                         IsThickLine(point, new Vec2(34f, 31f), new Vec2(10f, 8f), 3.6f) ||
                         IsPointInPolygon(point, new[]
                         {
                             new Vec2(6f, 5f),
                             new Vec2(10f, 21f),
                             new Vec2(21f, 10f)
                         });
    bool impact = IsFivePointStarPixel(point, new Vec2(34f, 33f), 10f, 4.3f);

    if (impact)
    {
        return IconTone.Accent;
    }

    if (reflectedPath)
    {
        return IconTone.Secondary;
    }

    return shield ? IconTone.Primary : IconTone.Clear;
}

static bool IsInterferencePixel(int x, int y, out bool isBorder)
{
    isBorder = false;
    Vec2 point = new(x + 0.5f, y + 0.5f);
    bool wave1 = point.X >= 8f &&
                 point.X <= 56f &&
                 MathF.Abs(point.Y - (22f + 5f * MathF.Sin((point.X - 8f) * 0.22f))) <= 2.3f;
    bool wave2 = point.X >= 8f &&
                 point.X <= 56f &&
                 MathF.Abs(point.Y - (36f + 5f * MathF.Sin((point.X - 8f) * 0.22f + MathF.PI))) <= 2.3f;
    return wave1 || wave2;
}

static bool IsAdrenalineDrainPixel(int x, int y, out bool isBorder)
{
    isBorder = false;
    Vec2 point = new(x + 0.5f, y + 0.5f);
    bool pulse = IsThickLine(point, new Vec2(6f, 40f), new Vec2(18f, 40f), 2.5f) ||
                 IsThickLine(point, new Vec2(18f, 40f), new Vec2(24f, 50f), 2.5f) ||
                 IsThickLine(point, new Vec2(24f, 50f), new Vec2(31f, 28f), 2.5f) ||
                 IsThickLine(point, new Vec2(31f, 28f), new Vec2(38f, 40f), 2.5f) ||
                 IsThickLine(point, new Vec2(38f, 40f), new Vec2(49f, 40f), 2.5f);
    bool shaft = IsThickLine(point, new Vec2(49f, 52f), new Vec2(49f, 20f), 3f);
    bool arrow = IsPointInPolygon(point, new[]
    {
        new Vec2(37f, 25f),
        new Vec2(61f, 25f),
        new Vec2(49f, 10f)
    });
    return pulse || shaft || arrow;
}

static bool IsCorrosivePixel(int x, int y, out bool isAccent)
{
    IconTone tone = GetCorrosiveTone(x, y);
    isAccent = tone == IconTone.Accent;
    return tone != IconTone.Clear;
}

static IconTone GetCorrosiveTone(int x, int y)
{
    Vec2 point = new(x + 0.5f, y + 0.5f);
    bool outer = IsPointInPolygon(point, new[]
    {
        new Vec2(14f, 54f),
        new Vec2(43f, 54f),
        new Vec2(56f, 34f),
        new Vec2(47f, 11f),
        new Vec2(18f, 10f),
        new Vec2(6f, 31f)
    });
    bool centerHole = Distance(point, new Vec2(31f, 32f)) <= 13f;
    bool brokenEdge = Distance(point, new Vec2(54f, 33f)) <= 6.5f ||
                      Distance(point, new Vec2(48f, 50f)) <= 4.5f ||
                      Distance(point, new Vec2(49f, 14f)) <= 4.5f;
    bool body = outer && !centerHole && !brokenEdge;
    if (!body)
    {
        return IconTone.Clear;
    }

    bool oxide = Distance(point, new Vec2(43f, 47f)) <= 2.5f ||
                 Distance(point, new Vec2(48f, 40f)) <= 2.5f ||
                 Distance(point, new Vec2(43f, 20f)) <= 2.5f ||
                 Distance(point, new Vec2(47f, 16f)) <= 2.2f;
    if (oxide)
    {
        return IconTone.Secondary;
    }

    bool rust = Distance(point, new Vec2(44f, 44f)) <= 9f ||
                Distance(point, new Vec2(45f, 19f)) <= 8f ||
                point.X >= 49f;
    if (rust)
    {
        return IconTone.Accent;
    }

    return IconTone.Primary;
}

static bool IsBlinkPixel(int x, int y, out bool isBorder)
{
    Vec2 point = new(x + 0.5f, y + 0.5f);
    Vec2 center = new(32f, 32f);
    float radius = Distance(point, center);
    bool portal = radius >= 17f && radius <= 23f && point.X >= 14f;
    bool dash = IsThickLine(point, new Vec2(8f, 32f), new Vec2(35f, 32f), 3.5f);
    bool arrow = IsPointInPolygon(point, new[]
    {
        new Vec2(43f, 32f),
        new Vec2(30f, 22f),
        new Vec2(30f, 42f)
    });
    bool spark = Distance(point, new Vec2(52f, 48f)) <= 4f ||
                 Distance(point, new Vec2(50f, 16f)) <= 3f;
    isBorder = portal || arrow;
    return portal || dash || arrow || spark;
}

static bool IsUnflinchingPixel(int x, int y, out bool isStar)
{
    Vec2 point = new(x + 0.5f, y + 0.5f);
    Vec2 center = new(31.5f, 32f);
    Vec2 ellipsePoint = new(point.X - center.X, (point.Y - center.Y) * 1.5f);
    float ellipseRadius = MathF.Sqrt(ellipsePoint.X * ellipsePoint.X + ellipsePoint.Y * ellipsePoint.Y);
    float angle = MathF.Atan2(ellipsePoint.Y, ellipsePoint.X) * 180f / MathF.PI;
    bool upperArc = ellipseRadius is >= 20.5f and <= 27.5f && angle is >= 18f and <= 162f;
    bool lowerArc = ellipseRadius is >= 20.5f and <= 27.5f && angle is >= -162f and <= -18f;
    bool stars = IsFivePointStarPixel(point, new Vec2(18f, 21f), 10f, 4.5f) ||
                 IsFivePointStarPixel(point, new Vec2(45f, 46f), 9f, 4f);
    isStar = stars;
    return upperArc || lowerArc || stars;
}

static bool IsFivePointStarPixel(Vec2 point, Vec2 center, float outerRadius, float innerRadius)
{
    Vec2[] vertices = new Vec2[10];
    for (int index = 0; index < vertices.Length; index++)
    {
        float radius = index % 2 == 0 ? outerRadius : innerRadius;
        float angle = MathF.PI * 0.5f + index * MathF.PI / 5f;
        vertices[index] = center + new Vec2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
    }

    return IsPointInPolygon(point, vertices);
}

static bool IsAnchorPixel(int x, int y, out bool isBorder)
{
    isBorder = false;
    Vec2 point = new(x + 0.5f, y + 0.5f);
    Vec2 ringCenter = new(13f, 51f);
    float ringRadius = Distance(point, ringCenter);
    bool ring = ringRadius is >= 5.5f and <= 10f;
    bool shaft = IsThickLine(point, new Vec2(18f, 45f), new Vec2(46f, 17f), 3.8f);
    bool crossbar = IsThickLine(point, new Vec2(15f, 29f), new Vec2(35f, 49f), 3.6f);
    bool curvedArms = IsThickLine(point, new Vec2(17f, 9f), new Vec2(28f, 6f), 4f) ||
                      IsThickLine(point, new Vec2(28f, 6f), new Vec2(40f, 7f), 4f) ||
                      IsThickLine(point, new Vec2(40f, 7f), new Vec2(50f, 14f), 4f) ||
                      IsThickLine(point, new Vec2(50f, 14f), new Vec2(56f, 25f), 4f) ||
                      IsThickLine(point, new Vec2(56f, 25f), new Vec2(58f, 38f), 4f) ||
                      IsThickLine(point, new Vec2(58f, 38f), new Vec2(56f, 47f), 4f);
    bool leftFluke = IsPointInPolygon(point, new[]
    {
        new Vec2(6f, 15f),
        new Vec2(22f, 12f),
        new Vec2(18f, 3f)
    });
    bool upperFluke = IsPointInPolygon(point, new[]
    {
        new Vec2(50f, 59f),
        new Vec2(54f, 41f),
        new Vec2(63f, 43f)
    });
    return ring || shaft || crossbar || curvedArms || leftFluke || upperFluke;
}

static bool IsChameleonPixel(int x, int y, out bool isAccent)
{
    Vec2 point = new(x + 0.5f, y + 0.5f);
    Vec2 tailCenter = new(17f, 31f);
    float tailRadius = Distance(point, tailCenter);
    float tailAngle = MathF.Atan2(point.Y - tailCenter.Y, point.X - tailCenter.X);
    float tailTarget = 4f + Repeat(tailAngle + MathF.PI, MathF.PI * 2f) / (MathF.PI * 2f) * 10f;
    bool tail = tailRadius is >= 3f and <= 16f && MathF.Abs(tailRadius - tailTarget) <= 2.8f;
    bool body = IsThickLine(point, new Vec2(21f, 33f), new Vec2(43f, 37f), 5.5f);
    bool head = Distance(point, new Vec2(49f, 39f)) <= 8f;
    bool frontLeg = IsThickLine(point, new Vec2(39f, 34f), new Vec2(47f, 23f), 2.5f);
    bool backLeg = IsThickLine(point, new Vec2(28f, 32f), new Vec2(22f, 21f), 2.5f);
    bool eye = Distance(point, new Vec2(51f, 42f)) <= 2.6f;
    isAccent = eye || tail;
    return tail || body || head || frontLeg || backLeg || eye;
}

static bool IsBlamerPixel(int x, int y, out bool isAccent)
{
    IconTone tone = GetBlamerTone(x, y);
    isAccent = tone == IconTone.Accent;
    return tone != IconTone.Clear;
}

static IconTone GetBlamerTone(int x, int y)
{
    const float scale = 0.82f;
    Vec2 point = new(
        32f + (x + 0.5f - 32f) / scale,
        32f + (y + 0.5f - 32f) / scale);
    bool bell = IsPointInPolygon(point, new[]
    {
        new Vec2(32f, 57f),
        new Vec2(24f, 55f),
        new Vec2(19f, 50f),
        new Vec2(17f, 43f),
        new Vec2(17f, 31f),
        new Vec2(15f, 24f),
        new Vec2(11f, 18f),
        new Vec2(10f, 14f),
        new Vec2(13f, 11f),
        new Vec2(51f, 11f),
        new Vec2(54f, 14f),
        new Vec2(53f, 18f),
        new Vec2(49f, 24f),
        new Vec2(47f, 31f),
        new Vec2(47f, 43f),
        new Vec2(45f, 50f),
        new Vec2(40f, 55f)
    }) || point.X is >= 29f and <= 35f && point.Y is >= 55f and <= 63f;
    bool rim = point.X is >= 11f and <= 53f && point.Y is >= 10f and <= 15f;
    bool clapper = Distance(point, new Vec2(32f, 7f)) <= 5f;
    float waveY = point.Y - 36f;
    float waveX = MathF.Abs(point.X - 32f);
    float innerWave = waveX * waveX / (26f * 26f) + waveY * waveY / (23f * 23f);
    float outerWave = waveX * waveX / (34f * 34f) + waveY * waveY / (30f * 30f);
    bool waves = waveX >= 21f && innerWave is >= 0.82f and <= 1.14f ||
                 waveX >= 28f && outerWave is >= 0.82f and <= 1.14f;

    if (waves)
    {
        return IconTone.Accent;
    }

    if (rim || clapper)
    {
        return IconTone.Secondary;
    }

    return bell ? IconTone.Primary : IconTone.Clear;
}

static bool IsThickLine(Vec2 point, Vec2 start, Vec2 end, float width)
{
    Vec2 axis = end - start;
    float length = axis.Magnitude;
    if (length <= 0f)
    {
        return false;
    }

    Vec2 direction = axis / length;
    Vec2 delta = point - start;
    float along = Dot(delta, direction);
    if (along < 0f || along > length)
    {
        return false;
    }

    float distance = MathF.Abs(Dot(delta, new Vec2(-direction.Y, direction.X)));
    return distance <= width;
}

static bool IsPointInPolygon(Vec2 point, IReadOnlyList<Vec2> polygon)
{
    bool inside = false;
    for (int current = 0, previous = polygon.Count - 1; current < polygon.Count; previous = current++)
    {
        Vec2 a = polygon[current];
        Vec2 b = polygon[previous];
        if ((a.Y > point.Y) != (b.Y > point.Y) &&
            point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
        {
            inside = !inside;
        }
    }

    return inside;
}

static bool IsSwordBladePixel(Vec2 point, out bool isBorder)
{
    isBorder = false;
    Vec2 start = new(18f, 16f);
    Vec2 tip = new(53f, 51f);
    Vec2 axis = tip - start;
    float length = axis.Magnitude;
    Vec2 direction = axis / length;
    Vec2 normal = new(-direction.Y, direction.X);
    Vec2 delta = point - start;
    float along = Dot(delta, direction);
    if (along < 0f || along > length)
    {
        return false;
    }

    float width = along > length - 8f
        ? Lerp(5f, 0.5f, (along - (length - 8f)) / 8f)
        : 5f;
    float side = MathF.Abs(Dot(delta, normal));
    if (side > width)
    {
        return false;
    }

    isBorder = side > width - 1.6f || along > length - 4f;
    return true;
}

static bool IsSwordGuardPixel(Vec2 point, out bool isBorder)
{
    isBorder = false;
    Vec2 center = new(18f, 16f);
    Vec2 axis = new Vec2(1f, 1f).Normalized;
    Vec2 normal = new(-axis.Y, axis.X);
    Vec2 delta = point - center;
    float along = Dot(delta, normal);
    float across = Dot(delta, axis);
    if (MathF.Abs(along) > 13f || MathF.Abs(across) > 2.6f)
    {
        return false;
    }

    isBorder = MathF.Abs(along) > 10.8f || MathF.Abs(across) > 1.2f;
    return true;
}

static bool IsSwordGripPixel(Vec2 point, out bool isBorder)
{
    isBorder = false;
    Vec2 start = new(7f, 5f);
    Vec2 end = new(18f, 16f);
    Vec2 axis = end - start;
    float length = axis.Magnitude;
    Vec2 direction = axis / length;
    Vec2 normal = new(-direction.Y, direction.X);
    Vec2 delta = point - start;
    float along = Dot(delta, direction);
    if (along < 0f || along > length)
    {
        return false;
    }

    float side = MathF.Abs(Dot(delta, normal));
    if (side > 3.4f)
    {
        return false;
    }

    isBorder = side > 2f;
    return true;
}

static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);

static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

static float Repeat(float value, float length) => value - MathF.Floor(value / length) * length;

static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;

static float Distance(Vec2 a, Vec2 b) => (a - b).Magnitude;

delegate bool IconShape(int x, int y, out bool isBorder);
delegate IconTone IconToneShape(int x, int y);

enum IconTone : byte
{
    Clear,
    Primary,
    Secondary,
    Accent
}

readonly record struct IconSpec(
    string Name,
    IconShape Shape,
    Color Color,
    Color? AccentColor = null,
    IconToneShape? ToneShape = null,
    Color? SecondaryColor = null);

readonly record struct Vec2(float X, float Y)
{
    public float Magnitude => MathF.Sqrt(X * X + Y * Y);
    public float SqrMagnitude => X * X + Y * Y;
    public Vec2 Normalized => Magnitude <= 0f ? this : this / Magnitude;
    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, float scalar) => new(a.X * scalar, a.Y * scalar);
    public static Vec2 operator /(Vec2 a, float scalar) => new(a.X / scalar, a.Y / scalar);
}
