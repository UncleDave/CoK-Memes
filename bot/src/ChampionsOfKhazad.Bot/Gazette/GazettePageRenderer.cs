using ChampionsOfKhazad.Bot.GenAi;
using SkiaSharp;

namespace ChampionsOfKhazad.Bot;

internal sealed class GazettePageRenderer : IGazettePageRenderer
{
    private const int Width = 1200;
    private const float Margin = 70;
    private const float Gap = 40;
    private static readonly SKColor Ink = new(42, 33, 25);
    private static readonly SKColor Paper = new(247, 239, 219);

    public GazettePage Render(GazetteEdition edition, long issueNumber, string dates, byte[]? illustration)
    {
        if (edition.Articles.Count is < 1 or > 3 || issueNumber < 1)
            throw new InvalidOperationException("Invalid newspaper layout input.");
        using var regular = OperatingSystem.IsWindows()
            ? SKTypeface.FromFamilyName("Georgia")
            : SKTypeface.FromFile("/usr/share/fonts/truetype/dejavu/DejaVuSerif.ttf");
        using var bold = OperatingSystem.IsWindows()
            ? SKTypeface.FromFamilyName("Georgia", SKFontStyle.Bold)
            : SKTypeface.FromFile("/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf");
        if (regular is null || bold is null)
            throw new InvalidOperationException("Newspaper serif fonts are unavailable.");
        using var body = new SKFont(regular, 30);
        using var headline = new SKFont(bold, 48);
        using var smallHeadline = new SKFont(bold, 36);
        using var small = new SKFont(regular, 22);
        using var masthead = new SKFont(bold, 65);
        using var paint = new SKPaint { Color = Ink, IsAntialias = true };
        using var rules = new SKPaint
        {
            Color = Ink,
            StrokeWidth = 2,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
        };
        using var art = DecodeIllustration(illustration);
        var contentWidth = Width - 2 * Margin;
        var leadWidth = art is null ? contentWidth : 660;
        var lead = edition.Articles[0];
        var leadTitle = Wrap(lead.Headline.ToUpperInvariant(), headline, contentWidth, paint);
        var leadBody = Wrap(lead.Body, body, leadWidth, paint);
        var columnWidth = (contentWidth - Gap) / 2;
        var secondaryWidth = edition.Articles.Count == 2 ? contentWidth : columnWidth;
        var secondary = edition
            .Articles.Skip(1)
            .Select(article => new
            {
                Title = Wrap(article.Headline, smallHeadline, secondaryWidth, paint),
                Body = Wrap(article.Body, body, secondaryWidth, paint),
            })
            .ToArray();
        var leadBodyHeight = Math.Max(leadBody.Count * 44, art is null ? 0 : 340);
        var secondaryHeight = secondary.Length == 0 ? 0 : secondary.Max(article => article.Title.Count * 48 + article.Body.Count * 44 + 65);
        var advert = Wrap(edition.Editorial, body, contentWidth - 50, paint);
        var height = 330 + leadTitle.Count * 60 + leadBodyHeight + secondaryHeight + advert.Count * 44 + 300;
        if (height > 4000)
            throw new InvalidOperationException("Newspaper layout is too tall.");
        using var bitmap = new SKBitmap(Width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(Paper);
        canvas.DrawRect(new SKRect(25, 25, Width - 25, height - 25), rules);
        while (masthead.MeasureText("THE KHAZAD GAZETTE", paint) > contentWidth)
            masthead.Size -= 1;
        Center(canvas, "THE KHAZAD GAZETTE", 115, masthead, paint);
        Center(canvas, "INDEPENDENT JOURNALISM. DEPENDENT ON GOSSIP.", 162, small, paint);
        canvas.DrawLine(Margin, 189, Width - Margin, 189, rules);
        canvas.DrawText($"Issue No. {issueNumber}", Margin, 225, small, paint);
        canvas.DrawText(dates, Width - Margin - small.MeasureText(dates, paint), 225, small, paint);
        canvas.DrawLine(Margin, 245, Width - Margin, 245, rules);
        var y = DrawLines(canvas, leadTitle, Margin, 310, 60, headline, paint) + 25;
        var bodyTop = y;
        DrawLines(canvas, leadBody, Margin, y, 44, body, paint);
        if (art is not null)
        {
            var rect = new SKRect(Width - Margin - 340, bodyTop - 30, Width - Margin, bodyTop + 310);
            using var monochrome = new SKPaint
            {
                IsAntialias = true,
                BlendMode = SKBlendMode.Multiply,
                ColorFilter = SKColorFilter.CreateColorMatrix([
                    .2126f,
                    .7152f,
                    .0722f,
                    0,
                    0,
                    .2126f,
                    .7152f,
                    .0722f,
                    0,
                    0,
                    .2126f,
                    .7152f,
                    .0722f,
                    0,
                    0,
                    0,
                    0,
                    0,
                    1,
                    0,
                ]),
            };
            canvas.DrawBitmap(art, rect, monochrome);
            canvas.DrawRect(rect, rules);
        }
        y = bodyTop + leadBodyHeight + 35;
        canvas.DrawLine(Margin, y, Width - Margin, y, rules);
        y += 60;
        for (var index = 0; index < secondary.Length; index++)
        {
            var article = secondary[index];
            var x = Margin + index * (columnWidth + Gap);
            var articleY = DrawLines(canvas, article.Title, x, y, 48, smallHeadline, paint) + 20;
            DrawLines(canvas, article.Body, x, articleY, 44, body, paint);
        }
        if (secondary.Length == 2)
            canvas.DrawLine(Width / 2, y - 35, Width / 2, y + secondaryHeight - 40, rules);
        y += secondaryHeight;
        canvas.DrawRect(new SKRect(Margin, y, Width - Margin, y + 95 + advert.Count * 44), rules);
        canvas.DrawText("CLASSIFIEDS", Margin + 25, y + 43, small, paint);
        DrawLines(canvas, advert, Margin + 25, y + 92, 44, body, paint);
        Center(canvas, "SELECTED DISPATCHES FROM CHAMPIONS OF KHAZAD", height - 55, small, paint);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        var bytes = encoded.ToArray();
        if (bytes.Length > 8_000_000)
            throw new InvalidOperationException("Newspaper image exceeds the attachment limit.");
        return new($"khazad-gazette-{issueNumber}.png", bytes);
    }

    private static SKBitmap? DecodeIllustration(byte[]? bytes)
    {
        if (bytes is null)
            return null;
        using var stream = new MemoryStream(bytes, writable: false);
        using var codec = SKCodec.Create(stream);
        if (codec is null || codec.Info.Width > 4096 || codec.Info.Height > 4096)
            throw new InvalidOperationException("Invalid editorial illustration.");
        return SKBitmap.Decode(codec) ?? throw new InvalidOperationException("Editorial illustration could not be decoded.");
    }

    internal static IReadOnlyList<string> Wrap(string text, SKFont font, float width, SKPaint paint)
    {
        var lines = new List<string>();
        var line = "";
        foreach (var word in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = line.Length == 0 ? word : line + " " + word;
            if (font.MeasureText(candidate, paint) <= width)
            {
                line = candidate;
                continue;
            }
            if (line.Length > 0)
                lines.Add(line);
            line = "";
            foreach (var rune in word.EnumerateRunes())
            {
                var next = line + rune;
                if (font.MeasureText(next, paint) > width && line.Length > 0)
                {
                    lines.Add(line);
                    line = "";
                }
                line += rune;
            }
        }
        if (line.Length > 0)
            lines.Add(line);
        return lines;
    }

    private static float DrawLines(SKCanvas canvas, IReadOnlyList<string> lines, float x, float y, float spacing, SKFont font, SKPaint paint)
    {
        foreach (var line in lines)
        {
            canvas.DrawText(line, x, y, font, paint);
            y += spacing;
        }
        return y;
    }

    private static void Center(SKCanvas canvas, string text, float y, SKFont font, SKPaint paint) =>
        canvas.DrawText(text, (Width - font.MeasureText(text, paint)) / 2, y, font, paint);
}
