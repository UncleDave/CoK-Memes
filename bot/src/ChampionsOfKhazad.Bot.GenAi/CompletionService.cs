namespace ChampionsOfKhazad.Bot.GenAi;

internal class CompletionService(
    LorekeeperPersonality lorekeeperPersonality,
    SycophantPersonality sycophantPersonality,
    ContrarianPersonality contrarianPersonality
) : ICompletionService
{
    public IPersonality Lorekeeper => lorekeeperPersonality;
    public IPersonality Sycophant => sycophantPersonality;
    public IPersonality Contrarian => contrarianPersonality;
}
