namespace ChampionsOfKhazad.Bot;

public sealed class GazetteSession
{
    public SemaphoreSlim Gate { get; } = new(1, 1);
    public GazettePendingDraft? Pending { get; set; }
    public GazetteCachedIllustration? Illustration { get; set; }
}
