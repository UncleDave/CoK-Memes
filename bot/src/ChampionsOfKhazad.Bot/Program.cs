using System.Globalization;
using AspNetMonsters.ApplicationInsights.AspNetCore;
using ChampionsOfKhazad.Bot;
using ChampionsOfKhazad.Bot.Core;
using ChampionsOfKhazad.Bot.RaidHelper;
using Discord;
using Discord.WebSocket;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI.Extensions;
using Serilog;
using Serilog.Events;

CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-GB");

var host = Host.CreateApplicationBuilder(args);

// csharpier-ignore
// ReSharper disable once MoveLocalFunctionAfterJumpStatement
LoggerConfiguration ConfigureLogger(LoggerConfiguration loggerConfiguration, IHostEnvironment hostEnvironment) =>
    loggerConfiguration
        .MinimumLevel.Is(hostEnvironment.IsDevelopment() ? LogEventLevel.Debug : LogEventLevel.Information)
        .Enrich.FromLogContext()
        .WriteTo.Console();

Log.Logger = ConfigureLogger(new LoggerConfiguration(), host.Environment).CreateBootstrapLogger();

host.Services.AddApplicationInsightsTelemetryWorkerService(options =>
{
    options.ConnectionString = host.Configuration.GetConnectionString("ApplicationInsights");
})
    .AddCloudRoleNameInitializer("Bot");

host.Services.AddSerilog(
    (provider, configuration) =>
    {
        ConfigureLogger(configuration, host.Environment)
            .WriteTo.ApplicationInsights(provider.GetRequiredService<TelemetryConfiguration>(), TelemetryConverter.Traces);
    }
);

host.Services.AddOptionsWithEagerValidation<BotOptions>(host.Configuration.GetSection(BotOptions.Key));

host.Services.AddSingleton<DiscordSocketClient>(services =>
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

// Must be the first AddMediatR invocation.
host.Services.AddMediatR(configuration =>
{
    configuration.RegisterServicesFromAssemblyContaining<Program>();
    configuration.NotificationPublisherType = typeof(ParallelNonBlockingPublisher);
    configuration.Lifetime = ServiceLifetime.Singleton;
});

host.Services.AddOpenAIService();

var openAiApiKey = host.Configuration.GetRequiredString("OpenAIServiceOptions:ApiKey");
var mongoConnectionString = host.Configuration.GetRequiredConnectionString("Mongo");

host.Services.AddGenAi(config =>
{
    config.OpenAiApiKey = openAiApiKey;
    config.MongoConnectionString = mongoConnectionString;
});

host.Services.AddBot(configuration =>
{
    configuration.Persistence.ConnectionString = mongoConnectionString;
})
    .AddGuildLore(configuration =>
    {
        configuration.EmbeddingsApiKey = host.Configuration.GetRequiredString("OpenAIServiceOptions:ApiKey");
    })
    .AddMongoPersistence()
    .AddDiscordStats()
    .AddMongoPersistence()
    .AddHardcoreStats()
    .AddMongoPersistence();

host.Services.AddSingleton<Assistant>();

host.Services.AddRaidHelperClient(host.Configuration.GetRequiredString("RaidHelper:ApiKey"));

host.Services.AddOptionsWithEagerValidation<EmoteStreakHandlerOptions>(host.Configuration.GetEventHandlerSection(EmoteStreakHandlerOptions.Key))
    .AddOptionsWithEagerValidation<SummonUserHandlerOptions>(host.Configuration.GetEventHandlerSection(SummonUserHandlerOptions.Key))
    .AddOptionsWithEagerValidation<ClownReactorOptions>(host.Configuration.GetEventHandlerSection(ClownReactorOptions.Key))
    .AddOptionsWithEagerValidation<QuestionMarkReactorOptions>(host.Configuration.GetEventHandlerSection(QuestionMarkReactorOptions.Key))
    .AddOptionsWithEagerValidation<HandOfSalvationReactorOptions>(host.Configuration.GetEventHandlerSection(HandOfSalvationReactorOptions.Key))
    .AddOptionsWithEagerValidation<MentionHandlerOptions>(host.Configuration.GetEventHandlerSection(MentionHandlerOptions.Key))
    .AddOptionsWithEagerValidation<HallOfFameReactionHandlerOptions>(host.Configuration.GetEventHandlerSection(HallOfFameReactionHandlerOptions.Key))
    .AddOptionsWithEagerValidation<RaidsSlashCommandOptions>(host.Configuration.GetSlashCommandSection(RaidsSlashCommandOptions.Key))
    .AddOptionsWithEagerValidation<SuggestSlashCommandOptions>(host.Configuration.GetSlashCommandSection(SuggestSlashCommandOptions.Key))
    .AddOptionsWithEagerValidation<AllFollowersOptions>(host.Configuration.GetSection(AllFollowersOptions.Key))
    .AddOptionsWithEagerValidation<SycophantFollowerOptions>(host.Configuration.GetFollowerSection(SycophantFollowerOptions.Key))
    .AddOptionsWithEagerValidation<StonerBroFollowerOptions>(host.Configuration.GetFollowerSection(StonerBroFollowerOptions.Key))
    .AddOptionsWithEagerValidation<NoNutNovemberExpertFollowerOptions>(host.Configuration.GetFollowerSection(NoNutNovemberExpertFollowerOptions.Key))
    .AddOptionsWithEagerValidation<RatFactsFollowerOptions>(host.Configuration.GetFollowerSection(RatFactsFollowerOptions.Key))
    .AddOptionsWithEagerValidation<HarassmentLawyerFollowerOptions>(host.Configuration.GetFollowerSection(HarassmentLawyerFollowerOptions.Key))
    .AddOptionsWithEagerValidation<NumberwangFollowerOptions>(host.Configuration.GetFollowerSection(NumberwangFollowerOptions.Key));

host.Services.AddHostedService<BotService>()
    .AddSingleton<BotContextProvider>()
    .AddSingleton<BotContext>(serviceProvider =>
        serviceProvider.GetRequiredService<BotContextProvider>().BotContext ?? throw new InvalidOperationException("BotContext is not available")
    );

host.Build().Run();
