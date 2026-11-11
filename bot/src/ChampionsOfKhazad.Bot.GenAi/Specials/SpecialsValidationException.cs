namespace ChampionsOfKhazad.Bot.GenAi;

public sealed class SpecialsValidationException(string category, SpecialsValidationField? field = null, int? actualLength = null, int? limit = null)
    : InvalidOperationException("Specials response failed validation.")
{
    public string Category { get; } = category;
    public SpecialsValidationField? Field { get; } = field;
    public int? ActualLength { get; } = actualLength;
    public int? Limit { get; } = limit;
}
