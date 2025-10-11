using ChampionsOfKhazad.Bot.Core;
using ChampionsOfKhazad.Bot.Lore.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateApplicationBuilder(args);

host.Services.AddBot(configuration =>
    {
        configuration.Persistence.ConnectionString = host.Configuration.GetRequiredConnectionString("Mongo");
    })
    .AddEmbeddings(configuration =>
    {
        configuration.OpenAiApiKey = host.Configuration.GetRequiredString("OpenAi:ApiKey");
    })
    .AddGuildLore()
    .AddMongoPersistence();

var app = host.Build();

var loreStore = app.Services.GetRequiredService<IStoreLore>();

var lore = await loreStore.ReadLoreAsync();

foreach (var item in lore)
{
    await loreStore.UpsertLoreAsync(item);
}
