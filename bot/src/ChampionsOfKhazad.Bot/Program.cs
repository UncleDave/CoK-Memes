using System.Globalization;
using ChampionsOfKhazad.Bot;
using ChampionsOfKhazad.Bot.Core;
using ChampionsOfKhazad.Bot.EventLoop;
using ChampionsOfKhazad.Bot.GenAi;
using ChampionsOfKhazad.Bot.RaidHelper;
using Discord;
using Discord.WebSocket;
using MediatR.NotificationPublishers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Discord;

var culture = CultureInfo.GetCultureInfo("en-GB");

CultureInfo.CurrentCulture = culture;
CultureInfo.CurrentUICulture = culture;

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

Log.Logger = ConfigureLogger(new LoggerConfiguration(), host.Environment).CreateLogger();

host.Services.AddSerilog();

host.Services.AddOptionsWithEagerValidation<BotOptions>(host.Configuration.GetSection(BotOptions.Key));
host.Services.AddOptionsWithEagerValidation<DiscordMessageToolsOptions>(host.Configuration.GetSection(DiscordMessageToolsOptions.Key));
host.Services.AddOptionsWithEagerValidation<GazetteOptions>(host.Configuration.GetSection(GazetteOptions.Key));
host.Services.AddSingleton(typeof(CooldownTracker<>));

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
                | GatewayIntents.GuildMessageReactions
                | GatewayIntents.GuildVoiceStates
                | GatewayIntents.GuildMembers,
            LogLevel = LogSeverity.Debug,
        }
    )
);

// Must be the first AddMediatR invocation.
host.Services.AddMediatR(configuration =>
{
    configuration.RegisterServicesFromAssemblyContaining<Program>();
    configuration.NotificationPublisherType = typeof(TaskWhenAllPublisher);
    configuration.LicenseKey = host.Configuration.GetRequiredString("MediatR:LicenseKey");
});

host.Services.AddBot(configuration =>
    {
        configuration.Persistence.ConnectionString = host.Configuration.GetRequiredConnectionString("Mongo");
    })
    .AddEmbeddings(configuration =>
    {
        configuration.OpenAiApiKey = host.Configuration.GetRequiredString("OpenAIServiceOptions:ApiKey");
    })
    .AddGuildLore()
    .AddLoreMongoPersistence()
    .AddDiscordMemes()
    .AddDiscordMemesMongoPersistence()
    .AddGenAi<DiscordEmojiHandler>(configuration =>
    {
        // TODO: Move these to an object
        configuration.OpenAiApiKey = host.Configuration.GetRequiredString("OpenAIServiceOptions:ApiKey");
        configuration.AzureStorageAccountName = host.Configuration.GetRequiredString("AzureStorageAccountName");
        configuration.AzureStorageAccountAccessKey = host.Configuration.GetRequiredString("AzureStorageAccountAccessKey");
        configuration.ImageGeneration.DailyAllowances =
            host.Configuration.GetSection("ImageGeneration:DailyAllowances").Get<Dictionary<ulong, short>>() ?? new Dictionary<ulong, short>();
    })
    .AddGenAiMongoPersistence();

host.Services.AddRaidHelperClient(host.Configuration.GetRequiredString("RaidHelper:ApiKey"));

host.Services.AddOptionsWithEagerValidation<EmoteStreakHandlerOptions>(host.Configuration.GetEventHandlerSection(EmoteStreakHandlerOptions.Key))
    .AddOptionsWithEagerValidation<SummonUserHandlerOptions>(host.Configuration.GetEventHandlerSection(SummonUserHandlerOptions.Key))
    .AddOptionsWithEagerValidation<ClownReactorOptions>(host.Configuration.GetEventHandlerSection(ClownReactorOptions.Key))
    .AddOptionsWithEagerValidation<QuestionMarkReactorOptions>(host.Configuration.GetEventHandlerSection(QuestionMarkReactorOptions.Key))
    .AddOptionsWithEagerValidation<HandOfSalvationReactorOptions>(host.Configuration.GetEventHandlerSection(HandOfSalvationReactorOptions.Key))
    .AddOptionsWithEagerValidation<MentionHandlerOptions>(host.Configuration.GetEventHandlerSection(MentionHandlerOptions.Key))
    .AddOptionsWithEagerValidation<HallOfFameReactionHandlerOptions>(host.Configuration.GetEventHandlerSection(HallOfFameReactionHandlerOptions.Key))
    .AddOptionsWithEagerValidation<UserInfoHandlerOptions>(host.Configuration.GetEventHandlerSection(UserInfoHandlerOptions.Key))
    .AddOptionsWithEagerValidation<RaidsSlashCommandOptions>(host.Configuration.GetSlashCommandSection(RaidsSlashCommandOptions.Key))
    .AddOptionsWithEagerValidation<SuggestSlashCommandOptions>(host.Configuration.GetSlashCommandSection(SuggestSlashCommandOptions.Key))
    .AddOptionsWithEagerValidation<AllFollowersOptions>(host.Configuration.GetSection(AllFollowersOptions.Key))
    .AddOptionsWithEagerValidation<ConversationFollowerOptions>(host.Configuration.GetFollowerSection(ConversationFollowerOptions.Key))
    .AddOptionsWithEagerValidation<RatFactsFollowerOptions>(host.Configuration.GetFollowerSection(RatFactsFollowerOptions.Key))
    .AddOptionsWithEagerValidation<DirectMessageHandlerOptions>(host.Configuration.GetEventHandlerSection(DirectMessageHandlerOptions.Key));

// Hosted services stop in reverse order: disconnect Discord before draining the notification queue.
host.Services.AddOptionsWithEagerValidation<NotificationQueueOptions>(host.Configuration.GetSection(NotificationQueueOptions.Key));
host.Services.AddSingleton<NotificationQueue>().AddHostedService<NotificationQueueService>();

host.Services.AddHostedService<BotService>()
    .AddScoped<PersonalityDirectMessageCommand>()
    .AddScoped<NotebookDirectMessageCommand>()
    .AddScoped<LoreDirectMessageCommand>()
    .AddScoped<GazetteDirectMessageCommand>()
    .AddScoped<IGazetteGateway, DiscordGazetteGateway>()
    .AddScoped<IGazetteReaderAccess, DiscordGazetteReaderAccess>()
    .AddSingleton<IGazettePageRenderer, GazettePageRenderer>()
    .AddSingleton<GazetteSession>()
    .AddSingleton<LoreEditorSession>()
    .AddScoped<INotebookReviewer, DiscordNotebookReviewer>()
    .AddScoped<INotebookSourceReader, DiscordMessageService>()
    .AddScoped<INotebookBackgroundSourceReader, DiscordMessageService>()
    .AddScoped<LorekeeperChatHistoryBuilder>()
    .AddSingleton(serviceProvider => new SharedDiscordRestClient(serviceProvider.GetRequiredService<DiscordSocketClient>().Rest))
    .AddSingleton<BotContextProvider>()
    .AddScoped<IDiscordMessageService, DiscordMessageService>()
    .AddScoped<BotContext>(serviceProvider =>
        serviceProvider.GetRequiredService<BotContextProvider>().BotContext ?? throw new InvalidOperationException("BotContext is not available")
    );

host.Services.AddHostedService<EventLoopService>()
    .AddOptionsWithEagerValidation<EventLoopOptions>(host.Configuration.GetSection(EventLoopOptions.Key))
    .AddScoped<IEventLoopEvent, ExcludedFromVoiceEvent>()
    .AddOptionsWithEagerValidation<ExcludedFromVoiceEventOptions>(host.Configuration.GetEventLoopSection(ExcludedFromVoiceEventOptions.Key))
    .AddScoped<IEventLoopEvent, WordOfTheDayHintEvent>()
    .AddOptionsWithEagerValidation<WordOfTheDayHintEventOptions>(host.Configuration.GetEventLoopSection(WordOfTheDayHintEventOptions.Key))
    .AddScoped<IEventLoopEvent, WeekCheckEvent>()
    .AddOptionsWithEagerValidation<WeekCheckEventOptions>(host.Configuration.GetEventLoopSection(WeekCheckEventOptions.Key))
    .AddScoped<IEventLoopEvent, NotebookObservationEvent>()
    .AddOptionsWithEagerValidation<NotebookObserverOptions>(host.Configuration.GetEventLoopSection(NotebookObserverOptions.Key));

host.Build().Run();
