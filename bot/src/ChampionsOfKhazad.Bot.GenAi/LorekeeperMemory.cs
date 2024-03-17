using System.Runtime.CompilerServices;
using Microsoft.SemanticKernel.Memory;

namespace ChampionsOfKhazad.Bot.GenAi;

internal class LorekeeperMemory(ISemanticTextMemory memory)
{
    private const string Collection = "memories";

    public IAsyncEnumerable<Memory> SearchAsync(string input, CancellationToken cancellationToken = default) =>
        SearchAsync(input, x => x, cancellationToken);

    public async IAsyncEnumerable<T> SearchAsync<T>(
        string input,
        Func<Memory, T> selector,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var results = memory.SearchAsync(Collection, input, 10, 0, cancellationToken: cancellationToken);

        await foreach (var result in results)
            yield return selector(new Memory(result.Metadata.Id, result.Metadata.Text));
    }

    public Task SaveAsync(string key, string input, CancellationToken cancellationToken = default) =>
        memory.SaveInformationAsync(Collection, input, key, cancellationToken: cancellationToken);
}
