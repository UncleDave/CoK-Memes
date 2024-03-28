namespace ChampionsOfKhazad.Bot.GenAi;

public interface ICompletionService
{
    IPersonality Lorekeeper { get; }
    IPersonality Sycophant { get; }
}
