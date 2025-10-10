using ChampionsOfKhazad.Bot.Core;
using ChampionsOfKhazad.Bot.Lore.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

// Build host
var host = Host.CreateApplicationBuilder(args);

host.Configuration.AddUserSecrets<Program>();

// Build bot with proper extensions
host.Services.AddBot(configuration =>
    {
        configuration.Persistence.ConnectionString = host.Configuration.GetRequiredConnectionString("Mongo");
    })
    .AddEmbeddings(configuration =>
    {
        configuration.OpenAiApiKey = host.Configuration.GetRequiredString("OpenAIServiceOptions:ApiKey");
        configuration.Model = "text-embedding-3-small";
    })
    .AddGuildLore()
    .AddMongoPersistence();

var app = host.Build();

// Get services
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var embeddingsService = app.Services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
var mongoConnectionString = host.Configuration.GetRequiredConnectionString("Mongo");

// Connect to MongoDB
var mongoClient = new MongoClient(mongoConnectionString);
var database = mongoClient.GetDatabase(MongoUrl.Create(mongoConnectionString).DatabaseName ?? "cok");
var loreCollection = database.GetCollection<MongoDB.Bson.BsonDocument>("lore");

logger.LogInformation("Starting embeddings regeneration...");
logger.LogInformation("Connected to database: {DatabaseName}", database.DatabaseNamespace.DatabaseName);

// Get all lore documents
var allDocuments = await loreCollection.Find(MongoDB.Driver.FilterDefinition<MongoDB.Bson.BsonDocument>.Empty).ToListAsync();
logger.LogInformation("Found {Count} documents to process", allDocuments.Count);

int processed = 0;
int updated = 0;

foreach (var document in allDocuments)
{
    processed++;
    var name = document.GetValue("Name", "").AsString;
    var content = document.GetValue("Content", "").AsString;

    if (string.IsNullOrWhiteSpace(content))
    {
        logger.LogWarning("[{Processed}/{Total}] Skipping '{Name}' - empty content", processed, allDocuments.Count, name);
        continue;
    }

    logger.LogInformation("[{Processed}/{Total}] Processing '{Name}'...", processed, allDocuments.Count, name);

    try
    {
        // Generate embedding
        var embeddingResult = await embeddingsService.GenerateAsync(content);
        var embedding = embeddingResult.Vector.ToArray();

        // Update document with embedding
        var filter = MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", document["_id"]);
        var update = MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Update.Set(
            "Embedding",
            new MongoDB.Bson.BsonArray(embedding.Select(f => new MongoDB.Bson.BsonDouble(f)))
        );

        await loreCollection.UpdateOneAsync(filter, update);
        updated++;

        logger.LogInformation(
            "[{Processed}/{Total}] ✓ Updated '{Name}' (embedding size: {Size})",
            processed,
            allDocuments.Count,
            name,
            embedding.Length
        );
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "[{Processed}/{Total}] ✗ Error processing '{Name}'", processed, allDocuments.Count, name);
    }
}

logger.LogInformation("Completed! Processed {Processed} documents, updated {Updated} embeddings.", processed, updated);
