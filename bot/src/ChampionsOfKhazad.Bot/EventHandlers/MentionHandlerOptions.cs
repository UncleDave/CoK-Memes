namespace ChampionsOfKhazad.Bot;

public class MentionHandlerOptions
{
    public const string Key = "Mention";

    public IReadOnlyDictionary<ulong, ushort>? HourlyUserQuotas { get; init; }
}
