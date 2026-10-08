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
        Assert.Contains("woodcut/engraving", prompt);
        Assert.Contains("A ceremonial phone resurrection.", prompt);
    }
}
