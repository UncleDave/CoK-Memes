namespace ChampionsOfKhazad.Bot.GenAi;

public interface ICompletionService
{
    IPersonality Lorekeeper { get; }
    IPersonality Sycophant { get; }
    IPersonality Contrarian { get; }
    IPersonality DisappointedTeacher { get; }
    IPersonality CondescendingTeacher { get; }
}
