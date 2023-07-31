namespace ChampionsOfKhazad.Bot;

public static class EnumerableExtensions
{
    public static async Task<IEnumerable<T>> WhereAsync<T>(this IEnumerable<T> source, Func<T, Task<bool>> predicate)
    {
        var tasks = source.Select(async item => (item, await predicate(item)));
        var results = await Task.WhenAll(tasks);
        return results.Where(x => x.Item2).Select(x => x.item);
    }
}
