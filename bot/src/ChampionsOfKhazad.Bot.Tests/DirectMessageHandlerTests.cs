using System.Reflection;
using ChampionsOfKhazad.Bot.DiscordMemes.WordOfTheDay;
using ChampionsOfKhazad.Bot.GenAi;
using Discord;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot.Tests;

public class DirectMessageHandlerTests
{
    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(false, true, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, true, false)]
    public async Task OnlyHumanAdminInDmCanChangePersonality(bool admin, bool dm, bool bot, bool shouldSave)
    {
        var store = new MemoryStore();
        var handler = CreateHandler(store, new WordGetter());
        var message = CreateMessage(admin, dm, bot, "personality furious", []);
        await handler.Handle(new MessageReceived(message), TestContext.Current.CancellationToken);
        Assert.Equal(shouldSave, store.Setting is not null);
    }

    [Fact]
    public async Task AdminWordBackdoorStillWorks()
    {
        var replies = new List<string>();
        var getter = new WordGetter();
        var store = new MemoryStore();
        var handler = CreateHandler(store, getter);
        await handler.Handle(new MessageReceived(CreateMessage(true, true, false, "WORD", replies)), TestContext.Current.CancellationToken);
        Assert.Equal(["testword"], replies);
        Assert.Null(store.Setting);
    }

    [Theory]
    [InlineData("help")]
    [InlineData("HELP")]
    [InlineData("  HeLp  ")]
    public async Task AdminHelpListsCommandsWithoutAccessingPersistence(string content)
    {
        var replies = new List<string>();
        var handler = CreateHandler(null!, null!);
        await handler.Handle(new MessageReceived(CreateMessage(true, true, false, content, replies)), TestContext.Current.CancellationToken);

        var help = Assert.Single(replies);
        Assert.Contains("word —", help);
        Assert.Contains("personality list", help);
        Assert.Contains("baseline|grouchy|furious", help);
        Assert.Contains("personality reset", help);
        Assert.Contains("30m, 2h, 1d", help);
        Assert.Contains("personality furious 2h", help);
        Assert.Contains("@Lorekeeper you've had a stroke.", help);
        Assert.Contains("notebook show/discard", help);
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public async Task AdminHelpIsNotExposedOutsideHumanAdminDms(bool admin, bool dm, bool bot)
    {
        var replies = new List<string>();
        var handler = CreateHandler(null!, null!);
        await handler.Handle(new MessageReceived(CreateMessage(admin, dm, bot, "help", replies)), TestContext.Current.CancellationToken);
        Assert.DoesNotContain(replies, reply => reply.Contains("Admin DM commands:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(false, true, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, true, false)]
    public async Task OnlyHumanAdminInDmCanPauseNotebook(bool admin, bool dm, bool bot, bool shouldPause)
    {
        var notes = new NotebookStore();
        var notebook = new NotebookService(notes, null!, null!, null!, TimeProvider.System, NullLogger<NotebookService>.Instance);
        var handler = CreateHandler(
            new MemoryStore(),
            new WordGetter(),
            new NotebookDirectMessageCommand(notebook, TimeProvider.System, NullLogger<NotebookDirectMessageCommand>.Instance)
        );
        await handler.Handle(new MessageReceived(CreateMessage(admin, dm, bot, "notebook pause", [])), TestContext.Current.CancellationToken);
        Assert.Equal(shouldPause, notes.State.Paused);
    }

    private static DirectMessageHandler CreateHandler(MemoryStore store, WordGetter getter, NotebookDirectMessageCommand? notebookCommand = null) =>
        new(
            Options.Create(new DirectMessageHandlerOptions { AdminUserId = 1 }),
            getter,
            new PersonalityDirectMessageCommand(
                new LorekeeperPersonalityService(store, TimeProvider.System, NullLogger<LorekeeperPersonalityService>.Instance)
            ),
            notebookCommand!
        );

    private static IUserMessage CreateMessage(bool admin, bool dm, bool bot, string content, List<string> replies)
    {
        var user = Stub<IUser>(method =>
            method.Name switch
            {
                "get_Id" => admin ? 1UL : 2UL,
                "get_IsBot" => bot,
                _ => throw new NotSupportedException(method.Name),
            }
        );
        object? ChannelCall(MethodInfo method, object?[]? args)
        {
            if (method.Name != "SendMessageAsync")
                throw new NotSupportedException(method.Name);
            replies.Add((string)args![0]!);
            return Task.FromResult<IUserMessage>(null!);
        }
        IMessageChannel channel = dm ? Stub<IDMChannel>(ChannelCall) : Stub<ITextChannel>(ChannelCall);
        return Stub<IUserMessage>(method =>
            method.Name switch
            {
                "get_Channel" => channel,
                "get_Author" => user,
                "get_Content" or "get_CleanContent" => content,
                _ => throw new NotSupportedException(method.Name),
            }
        );
    }

    private static T Stub<T>(Func<MethodInfo, object?> invoke)
        where T : class => Stub<T>((method, _) => invoke(method));

    private static T Stub<T>(Func<MethodInfo, object?[]?, object?> invoke)
        where T : class
    {
        var proxy = DispatchProxy.Create<T, InterfaceStub>();
        ((InterfaceStub)(object)proxy).InvokeMethod = invoke;
        return proxy;
    }

    public class InterfaceStub : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> InvokeMethod { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => InvokeMethod(targetMethod!, args);
    }

    private sealed class MemoryStore : ILorekeeperPersonalityStore
    {
        public LorekeeperPersonalitySetting? Setting { get; private set; }

        public Task<LorekeeperPersonalitySetting?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Setting);

        public Task SaveAsync(LorekeeperPersonalitySetting setting, CancellationToken cancellationToken = default)
        {
            Setting = setting;
            return Task.CompletedTask;
        }
    }

    private sealed class NotebookStore : INotebookStore
    {
        public NotebookState State { get; private set; } = new();

        public Task<NotebookState> GetAsync(CancellationToken cancellationToken) => Task.FromResult(State);

        public Task<bool> TrySaveAsync(NotebookState state, CancellationToken cancellationToken)
        {
            State = state with { Revision = state.Revision + 1 };
            return Task.FromResult(true);
        }
    }

    private sealed class WordGetter : IGetTheWordOfTheDay
    {
        public Task<WordOfTheDay> GetWordOfTheDayAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new WordOfTheDay("testword", DateOnly.FromDateTime(DateTime.UtcNow)));

        public Task<WordOfTheDay?> GetMostRecentlyWonWordOfTheDayAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<WordOfTheDay?>(null);
    }
}
