namespace ChampionsOfKhazad.Bot.GenAi;

internal class CompletionService(
    LorekeeperPersonality lorekeeperPersonality,
    SycophantPersonality sycophantPersonality,
    ContrarianPersonality contrarianPersonality,
    DisappointedTeacherPersonality disappointedTeacherPersonality,
    CondescendingTeacherPersonality condescendingTeacherPersonality
) : ICompletionService
{
    public IPersonality Lorekeeper => lorekeeperPersonality;
    public IPersonality Sycophant => sycophantPersonality;
    public IPersonality Contrarian => contrarianPersonality;
    public IPersonality DisappointedTeacher => disappointedTeacherPersonality;
    public IPersonality CondescendingTeacher => condescendingTeacherPersonality;
}
