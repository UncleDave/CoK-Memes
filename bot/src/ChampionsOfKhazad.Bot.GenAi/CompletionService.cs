namespace ChampionsOfKhazad.Bot.GenAi;

internal class CompletionService(LorekeeperPersonality lorekeeperPersonality, SycophantPersonality sycophantPersonality) : ICompletionService
{
    public IPersonality Lorekeeper => lorekeeperPersonality;
    public IPersonality Sycophant => sycophantPersonality;
}
