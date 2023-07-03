using System.Text.Json;
using ChampionsOfKhazad.Bot.LoreUploader;
using ChampionsOfKhazad.Bot.OpenAi.Embeddings;
using ChampionsOfKhazad.Bot.Pinecone;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pinecone;

var hostBuilder = Host.CreateApplicationBuilder(args);

hostBuilder.Services.AddEmbeddingsService(
    hostBuilder.Configuration["OpenAi:ApiKey"]
        ?? throw new ApplicationException("OpenAi:ApiKey is required")
);

hostBuilder.Services.AddPinecone(
    hostBuilder.Configuration["Pinecone:ApiKey"]
        ?? throw new ApplicationException("Pinecone:ApiKey is required")
);

var host = hostBuilder.Build();

var loreFileContent = await File.ReadAllTextAsync(
    Path.Join(AppDomain.CurrentDomain.BaseDirectory, "Lore.json")
);

var lore = JsonSerializer.Deserialize<Lore>(loreFileContent);

if (lore is null)
    throw new ApplicationException("Failed to deserialize Lore.json");

var textEntries = new List<TextEntry>
{
    new("history", lore.History),
    new("rules", string.Join('\n', lore.Rules))
};

var termTextEntries = lore.Terms.Select(
    x => new TextEntry($"term-{x.Key.ToLowerInvariant()}", x.Value)
);

textEntries.AddRange(termTextEntries);

var memberTextEntries = lore.Members.Select(
    x =>
        new TextEntry(
            $"member-{x.Name.ToLowerInvariant()}",
            string.Join(
                '\n',
                "Guild Member\n",
                string.Join(
                    '\n',
                    typeof(Member)
                        .GetProperties()
                        .Where(prop => prop.GetValue(x) is not null)
                        .Select(prop => $"{prop.Name.Humanize()}: {prop.GetValue(x)}")
                )
            )
        )
);

textEntries.AddRange(memberTextEntries);

var embeddingsService = host.Services.GetRequiredService<EmbeddingsService>();
var embeddings = await embeddingsService.CreateEmbeddingsAsync(textEntries.ToArray());

var pineconeIndexService = host.Services.GetRequiredService<IndexService>();

const string indexName = "cok-lore";

var indexes = await pineconeIndexService.ListIndexesAsync();

if (!indexes.Contains(indexName))
    await pineconeIndexService.CreateIndexAsync(indexName);

var index = await pineconeIndexService.GetIndexAsync(indexName);
var vectors = embeddings.Select(
    x =>
        new Vector
        {
            Id = x.Id,
            Values = x.Vector,
            Metadata = new MetadataMap { { "text", x.Text } }
        }
);

await index.Upsert(vectors);

// TODO: Delete vectors that no longer exist
// TODO: Don't create embeddings for vectors that haven't changed
