namespace ChampionsOfKhazad.Bot.GenAi;

public sealed class SpecialsValidationException(string category) : InvalidOperationException("Specials response failed validation.")
{
    public string Category { get; } = category;
}
