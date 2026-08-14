using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot.GenAi.Embeddings;

internal sealed class DiagnosticEmbeddingGenerator(
    IEmbeddingGenerator<string, Embedding<float>> innerGenerator,
    ILogger<DiagnosticEmbeddingGenerator> logger
) : DelegatingEmbeddingGenerator<string, Embedding<float>>(innerGenerator)
{
    public override async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        var inputs = values as IReadOnlyCollection<string> ?? values.ToArray();

        try
        {
            return await base.GenerateAsync(inputs, options, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Embedding generation was cancelled for {InputCount} inputs.", inputs.Count);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Embedding generation failed for {InputCount} inputs containing {CharacterCount} characters.",
                inputs.Count,
                inputs.Sum(input => input.Length)
            );
            throw;
        }
    }
}
