using ChampionsOfKhazad.Bot.GenAi.Embeddings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using MongoDB.Driver;

// Build configuration
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

// Get configuration values
var mongoConnectionString = configuration.GetConnectionString("Mongo") ?? throw new InvalidOperationException("Mongo connection string not found");
var openAiApiKey = configuration["OpenAi:ApiKey"] ?? throw new InvalidOperationException("OpenAI API key not found");

// Build service collection
var services = new ServiceCollection();

services.AddKernel().AddOpenAITextEmbeddingGeneration("text-embedding-3-small", openAiApiKey);

services.AddScoped<IEmbeddingsService, EmbeddingsService>();

var serviceProvider = services.BuildServiceProvider();

// Connect to MongoDB
var mongoClient = new MongoClient(mongoConnectionString);
var database = mongoClient.GetDatabase(MongoUrl.Create(mongoConnectionString).DatabaseName ?? "cok");
var loreCollection = database.GetCollection<MongoDB.Bson.BsonDocument>("lore");

// Get embeddings service
var embeddingsService = serviceProvider.GetRequiredService<IEmbeddingsService>();

Console.WriteLine("Starting embeddings regeneration...");
Console.WriteLine($"Connected to database: {database.DatabaseNamespace.DatabaseName}");

// Get all lore documents
var allDocuments = await loreCollection.Find(MongoDB.Driver.FilterDefinition<MongoDB.Bson.BsonDocument>.Empty).ToListAsync();
Console.WriteLine($"Found {allDocuments.Count} documents to process");

int processed = 0;
int updated = 0;

foreach (var document in allDocuments)
{
    processed++;
    var name = document.GetValue("Name", "").AsString;
    var content = document.GetValue("Content", "").AsString;

    if (string.IsNullOrWhiteSpace(content))
    {
        Console.WriteLine($"[{processed}/{allDocuments.Count}] Skipping '{name}' - empty content");
        continue;
    }

    Console.WriteLine($"[{processed}/{allDocuments.Count}] Processing '{name}'...");

    try
    {
        // Generate embedding
        var embedding = await embeddingsService.CreateEmbeddingAsync(content);

        // Update document with embedding
        var filter = MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", document["_id"]);
        var update = MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Update.Set(
            "Embedding",
            new MongoDB.Bson.BsonArray(embedding.Select(f => new MongoDB.Bson.BsonDouble(f)))
        );

        await loreCollection.UpdateOneAsync(filter, update);
        updated++;

        Console.WriteLine($"[{processed}/{allDocuments.Count}] ✓ Updated '{name}' (embedding size: {embedding.Length})");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{processed}/{allDocuments.Count}] ✗ Error processing '{name}': {ex.Message}");
    }
}

Console.WriteLine();
Console.WriteLine($"Completed! Processed {processed} documents, updated {updated} embeddings.");
