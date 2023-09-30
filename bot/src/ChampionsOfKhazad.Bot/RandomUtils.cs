namespace ChampionsOfKhazad.Bot;

public static class RandomUtils
{
    public static T PickRandom<T>(IList<T> source)
    {
        if (!source.Any())
            throw new InvalidOperationException("Cannot pick random item from empty list");

        return source.Count == 1 ? source[0] : source[Random.Shared.Next(0, source.Count)];
    }

    public static string RandomOrdinal(int min, int max)
    {
        var value = Random.Shared.Next(min, max + 1);
        return value.ToOrdinal();
    }

    public static (bool Success, int Roll) Roll(ushort chance)
    {
        var roll = Random.Shared.Next(1, 101);
        var success = roll <= chance;

        return (success, roll);
    }
}
