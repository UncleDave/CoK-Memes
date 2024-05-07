namespace ChampionsOfKhazad.Bot;

public static class ListExtensions
{
    public static T PickRandom<T>(this IReadOnlyList<T> source) => RandomUtils.PickRandom(source);
}
