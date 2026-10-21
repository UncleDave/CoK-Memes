namespace ChampionsOfKhazad.Bot.GenAi;

public record CustomerDisciplineCustomer(ulong UserId, int StrikeCount)
{
    public bool IsBarred => StrikeCount >= 3;
}
