using ChampionsOfKhazad.Bot;
using ChampionsOfKhazad.Bot.ChatBot;
using ChampionsOfKhazad.Bot.OpenAi.Embeddings;
using ChampionsOfKhazad.Bot.Pinecone;
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

host.Services
    .AddEventHandler<DirectMessageHandler>()
    .AddEventHandler<EmoteStreakHandler, EmoteStreakHandlerOptions>(
        host.Configuration.GetEventHandlerSection(EmoteStreakHandlerOptions.Key)
    )
    .AddEventHandler<SummonUserHandler, SummonUserHandlerOptions>(
        host.Configuration.GetEventHandlerSection(SummonUserHandlerOptions.Key)
    )
    .AddEventHandler<ClownReactor, ClownReactorOptions>(
        host.Configuration.GetEventHandlerSection(ClownReactorOptions.Key)
    )
    .AddEventHandler<QuestionMarkReactor, QuestionMarkReactorOptions>(
        host.Configuration.GetEventHandlerSection(QuestionMarkReactorOptions.Key)
    )
    .AddEventHandler<MentionHandler, MentionHandlerOptions>(
        host.Configuration.GetEventHandlerSection(MentionHandlerOptions.Key)
    )
    .AddEventHandler<SycophantHandler, SycophantHandlerOptions>(
        host.Configuration.GetEventHandlerSection(SycophantHandlerOptions.Key)
    )
    .AddEventHandler<HallOfFameReactionHandler, HallOfFameReactionHandlerOptions>(
        host.Configuration.GetEventHandlerSection(HallOfFameReactionHandlerOptions.Key)
    );

host.Services.AddHostedService<BotService>();
host.Build().Run();
