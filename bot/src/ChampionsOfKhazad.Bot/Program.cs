using ChampionsOfKhazad.Bot;
using ChampionsOfKhazad.Bot.ChatBot;
using ChampionsOfKhazad.Bot.OpenAi.Embeddings;
using ChampionsOfKhazad.Bot.Pinecone;
using ChampionsOfKhazad.Bot.RaidHelper;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenAI.Extensions;
using Serilog;
using Serilog.Events;

var host = Host.CreateApplicationBuilder(args);

// csharpier-ignore
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Is(LogEventLevel.Debug)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

host.Logging.ClearProviders().AddSerilog();

host.Services.AddOptionsWithEagerValidation<BotOptions>(
    host.Configuration.GetSection(BotOptions.Key)
);

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
host.Services.AddEmbeddingsService(
    host.Configuration["OpenAIServiceOptions:ApiKey"]
        ?? throw new ApplicationException("OpenAIServiceOptions:ApiKey is required")
);

host.Services.AddPinecone(
    host.Configuration["Pinecone:ApiKey"]
        ?? throw new ApplicationException("Pinecone:ApiKey is required")
);

host.Services.AddSingleton<Assistant>();

host.Services.AddRaidHelperClient(
    host.Configuration["RaidHelper:ApiKey"]
        ?? throw new ApplicationException("RaidHelper:ApiKey is required")
);

host.Services
    .AddMessageReceivedEventHandler<DirectMessageHandler>()
    .AddMessageReceivedEventHandler<EmoteStreakHandler, EmoteStreakHandlerOptions>(
        host.Configuration.GetEventHandlerSection(EmoteStreakHandlerOptions.Key)
    )
    .AddMessageReceivedEventHandler<SummonUserHandler, SummonUserHandlerOptions>(
        host.Configuration.GetEventHandlerSection(SummonUserHandlerOptions.Key)
    )
    .AddMessageReceivedEventHandler<ClownReactor, ClownReactorOptions>(
        host.Configuration.GetEventHandlerSection(ClownReactorOptions.Key)
    )
    .AddMessageReceivedEventHandler<QuestionMarkReactor, QuestionMarkReactorOptions>(
        host.Configuration.GetEventHandlerSection(QuestionMarkReactorOptions.Key)
    )
    .AddMessageReceivedEventHandler<MentionHandler, MentionHandlerOptions>(
        host.Configuration.GetEventHandlerSection(MentionHandlerOptions.Key)
    )
    .AddMessageReceivedEventHandler<SycophantHandler, SycophantHandlerOptions>(
        host.Configuration.GetEventHandlerSection(SycophantHandlerOptions.Key)
    )
    .AddReactionAddedEventHandler<HallOfFameReactionHandler, HallOfFameReactionHandlerOptions>(
        host.Configuration.GetEventHandlerSection(HallOfFameReactionHandlerOptions.Key)
    )
    .AddSlashCommand<RaidsSlashCommand, RaidsSlashCommandOptions>(
        host.Configuration.GetSlashCommandSection(RaidsSlashCommandOptions.Key)
    );

host.Services
    .AddHostedService<BotService>()
    .AddSingleton<BotContextProvider>()
    .AddScoped<BotContext>(
        serviceProvider =>
            serviceProvider.GetRequiredService<BotContextProvider>().BotContext
            ?? throw new InvalidOperationException("BotContext is not available")
    );

Log.Logger.Debug(DateTime.Now.ToShortDateString());

host.Build().Run();
