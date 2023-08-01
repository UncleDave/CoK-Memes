namespace ChampionsOfKhazad.Bot;

public static class RandomUtils
{
    private static readonly Random Random = new();

    public static T PickRandom<T>(IList<T> source)
    {
        if (!source.Any())
            throw new InvalidOperationException("Cannot pick random item from empty list");

        return source.Count == 1 ? source[0] : source[Random.Shared.Next(0, source.Count)];
    }

    public static string RandomOrdinal(int min, int max)
    {
        var value = Random.Shared.Next(min, max + 1);

        if (value is 11 or 12 or 13)
            return $"{value}th";

        return (value % 10) switch
        {
            1 => $"{value}st",
            2 => $"{value}nd",
            3 => $"{value}rd",
            _ => $"{value}th"
        };
    }

    public static bool Roll(int chance) => Random.Shared.Next(1, 101) <= chance;
}
