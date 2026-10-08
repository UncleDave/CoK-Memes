using ChampionsOfKhazad.Bot.GenAi;
using SkiaSharp;

namespace ChampionsOfKhazad.Bot.Tests;

public class GazettePageRendererTests
{
    [Fact]
    public void NewspaperRendersReadableTextAndOptionalIllustrationAsABoundedPng()
    {
        var edition = new GazetteEdition(
            [
                new(
                    "Local man purchases replacement phone; original objects",
                    "A handset previously declared beyond salvation returned to service following the radical intervention of pressing the correct buttons. The purchase of its replacement has been referred to the Gazette's Department of Preventable Expenditure.",
                    ["source"]
                ),
                new(
                    "Boardroom crisis enters flooring phase",
                    "A minor spelling incident acquired sufficient administrative weight to require two GIFs. Crabslog addressed the board; the board declined to comment, being made of wood.",
                    ["source"]
                ),
                new(
                    "Guild optimism remains dangerously unregulated",
                    "The Gazette recommends that all expressions of confidence be accompanied by a refundable deposit. Investigations into the phrase 'surely this time' remain ongoing.",
                    ["source"]
                ),
            ],
            "Wanted: one competent clock. Previous applicants arrived tomorrow."
        );
        using var art = new SKBitmap(300, 300);
        using (var canvas = new SKCanvas(art))
        {
            canvas.Clear(new SKColor(247, 239, 219));
            using var paint = new SKPaint
            {
                Color = SKColors.Black,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 10,
            };
            canvas.DrawRoundRect(new SKRect(80, 35, 220, 265), 15, 15, paint);
            canvas.DrawCircle(150, 230, 10, paint);
        }
        using var image = SKImage.FromBitmap(art);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        var page = new GazettePageRenderer().Render(edition, 1, "1–8 October 2026", png.ToArray());
        using var result = SKBitmap.Decode(page.Png);
        Assert.Equal(1200, result.Width);
        Assert.InRange(result.Height, 800, 4000);
        Assert.InRange(page.Png.Length, 1000, 8_000_000);
        Assert.Equal("khazad-gazette-1.png", page.FileName);
        if (Environment.GetEnvironmentVariable("COK_GAZETTE_RENDER_SAMPLE") is { Length: > 0 } path)
            File.WriteAllBytes(path, page.Png);
    }

    [Fact]
    public void LongWordsAreWrappedWithoutLosingText()
    {
        using var font = new SKFont(SKTypeface.Default, 30);
        using var paint = new SKPaint();
        var word = new string('x', 200);
        var lines = GazettePageRenderer.Wrap(word, font, 100, paint);
        Assert.Equal(word, string.Concat(lines));
        Assert.All(lines, line => Assert.True(font.MeasureText(line, paint) <= 100));
    }

    [Fact]
    public void DatesAreHumanReadableAndUseGuildLocalCalendarWithoutTimezoneLabels()
    {
        var date = new DateTimeOffset(2026, 10, 8, 22, 30, 0, TimeSpan.Zero);
        Assert.Equal("2–9 October 2026", GazetteDirectMessageCommand.FormatDates(date.AddDays(-7), date));
        Assert.DoesNotContain("UTC", GazetteDirectMessageCommand.Render(new([new("Headline", "Story", ["source"])], "Ad"), date.AddDays(-7), date));
    }
}
