namespace ChampionsOfKhazad.Bot;

public sealed class SpecialsSession
{
    public SemaphoreSlim Gate { get; } = new(1, 1);
    public SpecialsPendingDraft? Pending { get; set; }
}
