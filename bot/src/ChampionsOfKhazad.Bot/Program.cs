using System.Globalization;
using ChampionsOfKhazad.Bot;
using ChampionsOfKhazad.Bot.ChatBot;
using ChampionsOfKhazad.Bot.Lore;
using ChampionsOfKhazad.Bot.RaidHelper;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenAI.Extensions;
using Serilog;
using Serilog.Events;

CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-GB");

var host = Host.CreateApplicationBuilder(args);

// csharpier-ignore
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Is(host.Environment.IsDevelopment() ? LogEventLevel.Debug : LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

host.Logging.ClearProviders().AddSerilog();

host.Services.AddOptionsWithEagerValidation<BotOptions>(host.Configuration.GetSection(BotOptions.Key));

host.Services.AddSingleton<DiscordSocketClient>(
    services =>
        ActivatorUtilities.CreateInstance<LoggingDiscordSocketClient>(
            services,
            new DiscordSocketConfig
            {
                MessageCacheSize = 100,
                GatewayIntents =
                    GatewayIntents.Guilds
                    | GatewayIntents.GuildMessages
                    | GatewayIntents.DirectMessages
                    | GatewayIntents.MessageContent
                    | GatewayIntents.GuildMessageReactions,
                LogLevel = LogSeverity.Debug
            }
        )
);

host.Services.AddOpenAIService();

host.Services
    .AddGuildLore(
        new GuildLoreOptions
        {
            EmbeddingsApiKey = host.Configuration.GetRequiredString("OpenAIServiceOptions:ApiKey"),
            VectorDatabaseApiKey = host.Configuration.GetRequiredString("Pinecone:ApiKey")
        }
    )
    .AddMongoPersistence(host.Configuration.GetRequiredConnectionString("Mongo"));

host.Services.AddSingleton<Assistant>();

host.Services.AddRaidHelperClient(host.Configuration.GetRequiredString("RaidHelper:ApiKey"));

host.Services.AddMediatR(configuration =>
{
    configuration.RegisterServicesFromAssembly(typeof(Program).Assembly);
    configuration.NotificationPublisherType = typeof(ParallelNonBlockingPublisher);
    configuration.Lifetime = ServiceLifetime.Singleton;
});

host.Services
    .AddOptionsWithEagerValidation<EmoteStreakHandlerOptions>(host.Configuration.GetEventHandlerSection(EmoteStreakHandlerOptions.Key))
    .AddOptionsWithEagerValidation<SummonUserHandlerOptions>(host.Configuration.GetEventHandlerSection(SummonUserHandlerOptions.Key))
    .AddOptionsWithEagerValidation<ClownReactorOptions>(host.Configuration.GetEventHandlerSection(ClownReactorOptions.Key))
    .AddOptionsWithEagerValidation<QuestionMarkReactorOptions>(host.Configuration.GetEventHandlerSection(QuestionMarkReactorOptions.Key))
    .AddOptionsWithEagerValidation<MentionHandlerOptions>(host.Configuration.GetEventHandlerSection(MentionHandlerOptions.Key))
    .AddOptionsWithEagerValidation<SycophantHandlerOptions>(host.Configuration.GetEventHandlerSection(SycophantHandlerOptions.Key))
    .AddOptionsWithEagerValidation<HallOfFameReactionHandlerOptions>(host.Configuration.GetEventHandlerSection(HallOfFameReactionHandlerOptions.Key))
    .AddOptionsWithEagerValidation<RaidsSlashCommandOptions>(host.Configuration.GetSlashCommandSection(RaidsSlashCommandOptions.Key));

host.Services
    .AddHostedService<BotService>()
    .AddSingleton<BotContextProvider>()
    .AddSingleton<BotContext>(
        serviceProvider =>
            serviceProvider.GetRequiredService<BotContextProvider>().BotContext ?? throw new InvalidOperationException("BotContext is not available")
    );

host.Build().Run();
