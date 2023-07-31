namespace ChampionsOfKhazad.Bot;

public static class TaskExtensions
{
    public static async Task<T> SingleAsync<T>(this Task<IReadOnlyCollection<T>> source, Func<T, bool> predicate) => (await source).Single(predicate);
}
