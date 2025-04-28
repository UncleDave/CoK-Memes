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
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Discord;

CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-GB");

var host = Host.CreateApplicationBuilder(args);

// ReSharper disable once MoveLocalFunctionAfterJumpStatement
LoggerConfiguration ConfigureLogger(LoggerConfiguration loggerConfiguration, IHostEnvironment hostEnvironment) =>
    loggerConfiguration
        .MinimumLevel.Is(hostEnvironment.IsDevelopment() ? LogEventLevel.Debug : LogEventLevel.Information)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.Discord(
            host.Configuration.GetValue<ulong>("DiscordSerilogSink:WebhookId"),
            host.Configuration.GetValue<string>("DiscordSerilogSink:WebhookToken"),
            restrictedToMinimumLevel: LogEventLevel.Error
        );

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
            LogLevel = LogSeverity.Debug,
        }
    )
);

// Must be the first AddMediatR invocation.
host.Services.AddMediatR(configuration =>
{
    configuration.RegisterServicesFromAssemblyContaining<Program>();
    configuration.NotificationPublisherType = typeof(ParallelNonBlockingPublisher);
});

var mongoConnectionString = host.Configuration.GetRequiredConnectionString("Mongo");

host.Services.AddGenAi<DiscordEmojiHandler>(config =>
{
    config.OpenAiApiKey = host.Configuration.GetRequiredString("OpenAIServiceOptions:ApiKey");
    config.MongoConnectionString = mongoConnectionString;
    config.GoogleSearchEngineId = host.Configuration.GetRequiredString("GoogleSearchEngine:Id");
    config.GoogleSearchEngineApiKey = host.Configuration.GetRequiredString("GoogleSearchEngine:ApiKey");
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
    .AddDiscordMemes()
    .AddMongoPersistence();

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
    .AddOptionsWithEagerValidation<TeacherFollowerOptions>(host.Configuration.GetFollowerSection(TeacherFollowerOptions.Key))
    .AddOptionsWithEagerValidation<GermanyBisFollowerOptions>(host.Configuration.GetFollowerSection(GermanyBisFollowerOptions.Key));

host.Services.AddHostedService<BotService>()
    .AddSingleton<BotContextProvider>()
    .AddScoped<BotContext>(serviceProvider =>
        serviceProvider.GetRequiredService<BotContextProvider>().BotContext ?? throw new InvalidOperationException("BotContext is not available")
    );

host.Build().Run();
