using OpenAI.Images;

namespace ChampionsOfKhazad.Bot.GenAi.Tests;

public class GazetteIllustratorTests
{
    [Fact]
    public void ArtPromptIsWordlessFictionalAndTreatsConceptAsQuotedData()
    {
        var prompt = GazetteIllustrator.BuildPrompt("A ceremonial phone resurrection.");
        Assert.Contains("NO text", prompt);
        Assert.Contains("untrusted subject data", prompt);
        Assert.Contains("identifiable real people", prompt);
        Assert.Contains("340-pixel thumbnail", prompt);
        Assert.Contains("ONE obvious visual punchline", prompt);
        Assert.Contains("two or three large essential props", prompt);
        Assert.Contains("Thick clean pen contours", prompt);
        Assert.Contains("no detailed rooms/workshops", prompt);
        Assert.Contains("A ceremonial phone resurrection.", prompt);
    }

    [Fact]
    public void GazetteUsesExplicitHighQualitySquarePngWithoutSwitchingTheSharedFlareModel()
    {
        var options = GazetteIllustrator.CreateOptions();
        Assert.Equal(GeneratedImageQuality.High, options.Quality);
        Assert.Equal(GeneratedImageSize.W1024xH1024, options.Size);
#pragma warning disable OPENAI001
        Assert.Equal(GeneratedImageFileFormat.Png, options.OutputFileFormat);
#pragma warning restore OPENAI001
        Assert.Equal("gpt-image-2.5-flare", Constants.DefaultImageModel);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void EmptyConceptsNeverBecomeImageRequests(string concept) =>
        Assert.Throws<InvalidOperationException>(() => GazetteIllustrator.BuildPrompt(concept));
}
