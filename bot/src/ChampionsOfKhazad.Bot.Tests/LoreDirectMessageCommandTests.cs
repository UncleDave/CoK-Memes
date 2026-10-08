using System.Text;
using System.Text.RegularExpressions;
using ChampionsOfKhazad.Bot.GenAi;
using ChampionsOfKhazad.Bot.Lore.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot.Tests;

public class LoreDirectMessageCommandTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ClearAdminRequestCreatesMemberWithUnknownDetailsAndUndoRemovesIt()
    {
        var fixture = new Fixture(Create(new() { Biography = "Dies to elevators." }));
        var reply = await fixture.Command.ExecuteAsync(1, "Add Grim, who keeps dying to elevators", Token);
        Assert.Contains("Saved Grim", reply);
        var entry = fixture.Store.State.Entry!;
        Assert.Equal("Unknown", entry.Pronouns);
        Assert.Equal("Unknown", entry.Nationality);
        Assert.Equal("Unknown", entry.MainCharacter);
        Assert.Equal("Dies to elevators.", entry.Biography);
        var revision = Assert.Single(fixture.Store.State.History);
        Assert.Equal(1UL, revision.ActorId);
        Assert.Equal("admin-dm", revision.Source);
        Assert.Null(revision.Before);
        Assert.Equal(entry, revision.After);

        Assert.Contains("undo", await fixture.Command.ExecuteAsync(1, "undo", Token));
        Assert.Null(fixture.Store.State.Entry);
        Assert.Contains("deleted", await fixture.Command.ExecuteAsync(1, "lore show Grim", Token));
        Assert.Contains("create", await fixture.Command.ExecuteAsync(1, "lore history Grim", Token));
        await fixture.Command.ExecuteAsync(1, "lore undo Grim", Token);
        Assert.Equal(entry, fixture.Store.State.Entry);
        Assert.Equal(1, fixture.Planner.Calls);
    }

    [Fact]
    public async Task FocusedUpdatesPreserveAllUnspecifiedFields()
    {
        var original = Member() with { Aliases = ["Grimbles"], Roles = ["Raider"] };
        var fixture = new Fixture(Update(new() { MainCharacter = "Paladin" }), original);
        var reply = await fixture.Command.ExecuteAsync(1, "Grim's main is actually a paladin now. Keep the elevator joke.", Token);
        Assert.Contains("Saved Grim", reply);
        Assert.Contains("Shaman → Paladin", reply);
        Assert.Equal(original with { MainCharacter = "Paladin" }, fixture.Store.State.Entry);
        Assert.Equal(original, fixture.Store.State.History[^1].Before);
        await fixture.Command.ExecuteAsync(1, "undo", Token);
        Assert.Equal(original, fixture.Store.State.Entry);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("clarify")]
    public async Task ChatterAndAmbiguousRequestsDoNotWriteAndClarificationsHaveContext(string action)
    {
        var fixture = new Fixture(new(action, null, null, new(), "Which member do you mean?", false), Member());
        Assert.Equal("Which member do you mean?", await fixture.Command.ExecuteAsync(1, "Grim?", Token));
        Assert.Equal(0, fixture.Store.Saves);
        fixture.Planner.Plan = Update(new() { MainCharacter = "Paladin" });
        await fixture.Command.ExecuteAsync(1, "Grimbles, change his main to paladin", Token);
        Assert.Equal("Grim?", Assert.Single(fixture.Planner.Conversation!).Request);
        Assert.Equal("Paladin", fixture.Store.State.Entry!.MainCharacter);
    }

    [Fact]
    public async Task OtherUsersCannotReadLoreOrCallTheModel()
    {
        var fixture = new Fixture(Create(new() { Biography = "New" }));
        Assert.Null(await fixture.Command.ExecuteAsync(2, "Add Grim", Token));
        Assert.Null(await fixture.Command.ExecuteAsync(2, "lore history Grim", Token));
        Assert.Equal(0, fixture.Planner.Calls);
        Assert.Equal(0, fixture.Store.Reads);
        Assert.Equal(0, fixture.Store.Saves);
    }

    [Fact]
    public async Task DeletionAlwaysRequiresAnExactSingleUseConfirmationAndCanBeUndone()
    {
        var fixture = new Fixture(new("delete", "Grim", "member", new(), "Delete Grim", false), Member());
        var reply = await fixture.Command.ExecuteAsync(1, "Delete Grim's lore", Token);
        Assert.Contains("Not saved yet", reply);
        Assert.Contains("Elevator joke", reply);
        Assert.Equal(0, fixture.Store.Saves);
        Assert.Contains("exact", await fixture.Command.ExecuteAsync(1, "lore confirm wrong", Token));
        Assert.Equal(0, fixture.Store.Saves);
        var confirm = Confirmation(reply!);
        Assert.Contains("Saved Grim", await fixture.Command.ExecuteAsync(1, confirm, Token));
        Assert.Null(fixture.Store.State.Entry);
        Assert.Contains("No live", await fixture.Command.ExecuteAsync(1, confirm, Token));
        Assert.Equal(1, fixture.Store.Saves);
        await fixture.Command.ExecuteAsync(1, "undo", Token);
        Assert.Equal(Member(), fixture.Store.State.Entry);
    }

    [Fact]
    public async Task ConfirmationCannotOverwriteAConcurrentPortalEdit()
    {
        var fixture = new Fixture(new("delete", "Grim", "member", new(), "Delete Grim", true), Member());
        var reply = await fixture.Command.ExecuteAsync(1, "Delete Grim", Token);
        fixture.Store.ConcurrentEdit(Member() with { Biography = "Portal correction" });
        Assert.Contains("Nothing was overwritten", await fixture.Command.ExecuteAsync(1, Confirmation(reply!), Token));
        Assert.Equal("Portal correction", fixture.Store.State.Entry!.Biography);
    }

    [Fact]
    public async Task ChangesDuringPlanningFailClosedWithoutLosingNewerLore()
    {
        var fixture = new Fixture(Update(new() { MainCharacter = "Paladin" }), Member());
        fixture.Planner.OnPlan = () => fixture.Store.ConcurrentEdit(Member() with { MainCharacter = "Mage" });
        Assert.Contains("Nothing was overwritten", await fixture.Command.ExecuteAsync(1, "Update Grim", Token));
        Assert.Equal("Mage", fixture.Store.State.Entry!.MainCharacter);
        Assert.Equal(0, fixture.Store.Saves);
    }

    [Fact]
    public async Task CompareExchangeFailureDoesNotClaimSuccess()
    {
        var fixture = new Fixture(Update(new() { MainCharacter = "Paladin" }), Member());
        fixture.Store.Reject = true;
        Assert.Contains("Nothing was overwritten", await fixture.Command.ExecuteAsync(1, "Update Grim", Token));
        Assert.Equal("Shaman", fixture.Store.State.Entry!.MainCharacter);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BroadRewritesRequireConfirmationFromModelFlagOrDeterministicGuard(bool modelFlag)
    {
        var original = Member() with { Biography = new string('x', 500) };
        var fixture = new Fixture(
            Update(new() { Biography = modelFlag ? new string('y', 500) : "Short replacement" }) with
            {
                RequiresConfirmation = modelFlag,
            },
            original
        );
        Assert.Contains("Not saved yet", await fixture.Command.ExecuteAsync(1, "Rewrite Grim", Token));
        Assert.Equal(0, fixture.Store.Saves);
    }

    [Fact]
    public async Task ExpiredOrCancelledConfirmationsDoNotWrite()
    {
        var fixture = new Fixture(new("delete", "Grim", "member", new(), "Delete Grim", true), Member());
        var first = await fixture.Command.ExecuteAsync(1, "Delete Grim", Token);
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(10);
        Assert.Contains("No live", await fixture.Command.ExecuteAsync(1, Confirmation(first!), Token));
        var second = await fixture.Command.ExecuteAsync(1, "Delete Grim", Token);
        await fixture.Command.ExecuteAsync(1, "lore cancel", Token);
        Assert.Contains("No live", await fixture.Command.ExecuteAsync(1, Confirmation(second!), Token));
        Assert.Equal(0, fixture.Store.Saves);
    }

    [Fact]
    public async Task NewNaturalRequestInvalidatesEarlierConfirmation()
    {
        var fixture = new Fixture(new("delete", "Grim", "member", new(), "Delete Grim", true), Member());
        var first = await fixture.Command.ExecuteAsync(1, "Delete Grim", Token);
        fixture.Planner.Plan = new("none", null, null, new(), "Hello", false);
        await fixture.Command.ExecuteAsync(1, "Actually never mind", Token);
        Assert.Contains("No live", await fixture.Command.ExecuteAsync(1, Confirmation(first!), Token));
        Assert.Equal(0, fixture.Store.Saves);
    }

    [Fact]
    public async Task SessionsExpireAndResetWithoutDeletingSavedHistory()
    {
        var fixture = new Fixture(new("clarify", null, null, new(), "Which Grim?", false), Member());
        await fixture.Command.ExecuteAsync(1, "Update Grim", Token);
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(30);
        await fixture.Command.ExecuteAsync(1, "The shaman", Token);
        Assert.Empty(fixture.Planner.Conversation!);
        await fixture.Command.ExecuteAsync(1, "lore reset", Token);
        fixture.Planner.Plan = Update(new() { MainCharacter = "Paladin" });
        await fixture.Command.ExecuteAsync(1, "Update Grim's main", Token);
        // Reset's own response is harmless context, but the old editing conversation is gone.
        Assert.DoesNotContain(fixture.Planner.Conversation!, turn => turn.Request == "Update Grim");
        await fixture.Command.ExecuteAsync(1, "lore reset", Token);
        Assert.Single(fixture.Store.State.History);
    }

    [Fact]
    public async Task CreateCannotDuplicateAnExistingAliasAndUpdatesCannotChangeType()
    {
        var fixture = new Fixture(Create(new() { Biography = "New" }) with { Name = "Grimbles" }, Member() with { Aliases = ["Grimbles"] });
        Assert.Contains("already belongs", await fixture.Command.ExecuteAsync(1, "Add Grimbles", Token));
        fixture.Planner.Plan = new("update", "Grim", "guild", new() { Content = "Wrong type" }, "Edit", false);
        Assert.Contains("exact existing", await fixture.Command.ExecuteAsync(1, "Update Grim", Token));
        Assert.Equal(0, fixture.Store.Saves);
    }

    [Fact]
    public async Task BareUndoDoesNotRevertSomeoneElsesLaterChange()
    {
        var fixture = new Fixture(Update(new() { MainCharacter = "Paladin" }), Member());
        await fixture.Command.ExecuteAsync(1, "Update Grim", Token);
        fixture.Store.ConcurrentEdit(Member() with { MainCharacter = "Mage" });
        Assert.Contains("changed since", await fixture.Command.ExecuteAsync(1, "undo", Token));
        Assert.Equal("Mage", fixture.Store.State.Entry!.MainCharacter);
    }

    [Fact]
    public async Task FailureDoesNotClaimASaveAndCancelledCallsDoNotWrite()
    {
        var fixture = new Fixture(Update(new() { MainCharacter = "Paladin" }), Member());
        fixture.Planner.OnPlan = () => throw new InvalidOperationException("AI failed");
        Assert.Contains("could not be confirmed", await fixture.Command.ExecuteAsync(1, "Update Grim", Token));
        Assert.Equal(0, fixture.Store.Saves);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Command.ExecuteAsync(1, "Update Grim", cancellation.Token));
        Assert.Equal(0, fixture.Store.Saves);
    }

    [Fact]
    public async Task GuildJokesCanBeCreatedAndAmendedWithoutLosingOriginalExplanation()
    {
        var fixture = new Fixture(
            new("create", "Floor inspector", "guild", new() { Content = "A joke about Leaf dying on pull." }, "Create joke", false)
        );
        await fixture.Command.ExecuteAsync(1, "Add the floor inspector joke", Token);
        Assert.Equal("guild", fixture.Store.State.Entry!.Kind);
        fixture.Planner.Plan = new(
            "update",
            "Floor inspector",
            "guild",
            new() { Content = "A joke about Leaf dying on pull. Started during the first raid." },
            "Update joke",
            false
        );
        Assert.Contains("Saved", await fixture.Command.ExecuteAsync(1, "Add that it started in the first raid", Token));
        Assert.Contains("Leaf dying on pull", fixture.Store.State.Entry.Content);
        Assert.Equal(2, fixture.Store.State.History.Count);
    }

    [Fact]
    public async Task OverlappingDmsAreSerializedAndLaterPlansSeeEarlierSavedChanges()
    {
        var store = new MemoryStore(Member());
        var planner = new BlockingPlanner();
        var command = new LoreDirectMessageCommand(
            store,
            planner,
            new(),
            Options.Create(new DirectMessageHandlerOptions { AdminUserId = 1 }),
            new Clock(),
            NullLogger<LoreDirectMessageCommand>.Instance
        );
        var first = command.ExecuteAsync(1, "Change Grim's main to paladin", Token);
        await planner.Started.Task.WaitAsync(Token);
        var second = command.ExecuteAsync(1, "Add a second joke about Grim", Token);
        Assert.Equal(1, planner.Calls);
        planner.Release.SetResult();
        Assert.Contains("Saved", await first);
        Assert.Contains("Saved", await second);
        Assert.Equal("Paladin", planner.SecondInput!.MainCharacter);
        Assert.Equal("Paladin", store.State.Entry!.MainCharacter);
        Assert.Equal("Elevator joke. Another joke.", store.State.Entry.Biography);
    }

    [Fact]
    public async Task IdenticalUpdatesDoNotCreateRevisions()
    {
        var fixture = new Fixture(Update(new() { MainCharacter = "Shaman" }), Member());
        Assert.Contains("No changes needed", await fixture.Command.ExecuteAsync(1, "Set Grim's main to shaman", Token));
        Assert.Equal(0, fixture.Store.Saves);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LongTextAppendShowsTheEntireAdditionInSaveHistoryAndUndo(bool member)
    {
        var original = "Earlier history: " + new string('h', 600) + ".";
        var addition = " The guild has finished its MoP era. " + new string('n', 300) + " Horde PvE launch in November.";
        var entry = member ? Member() with { Biography = original } : new LoreEntrySnapshot("History", "guild") { Content = original };
        var changes = member ? new LoreEditChanges { Biography = original + addition } : new LoreEditChanges { Content = original + addition };
        var fixture = new Fixture(new("update", entry.Name, entry.Kind, changes, "Append new history", false), entry);

        var saved = await fixture.Command.ExecuteAsync(1, "Add the latest history", Token);
        Assert.Contains(NotebookReview.DisplayText(addition), saved);
        Assert.DoesNotContain(NotebookReview.DisplayText(original), saved);
        Assert.Contains(NotebookReview.DisplayText(addition), await fixture.Command.ExecuteAsync(1, $"lore history {entry.Name}", Token));
        Assert.Contains(NotebookReview.DisplayText(original + addition), await fixture.Command.ExecuteAsync(1, $"lore show {entry.Name}", Token));

        var undone = await fixture.Command.ExecuteAsync(1, "undo", Token);
        Assert.Contains(NotebookReview.DisplayText(addition), undone);
        Assert.DoesNotContain(NotebookReview.DisplayText(original), undone);
        Assert.Equal(entry, fixture.Store.State.Entry);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LongTextInsertionShowsNewTextAtTheBeginningOrInTheMiddle(bool prepend)
    {
        var prefix = new string('p', 500);
        var suffix = new string('s', 500);
        var addition = " Newly recorded incident. ";
        var original = prefix + suffix;
        var updated = prepend ? addition + original : prefix + addition + suffix;
        var fixture = GuildTextUpdate(original, updated);

        var reply = await fixture.Command.ExecuteAsync(1, "Add the incident", Token);
        Assert.Contains(NotebookReview.DisplayText(addition), reply);
        Assert.DoesNotContain(prefix, reply);
        Assert.DoesNotContain(suffix, reply);
        Assert.Equal(updated, fixture.Store.State.Entry!.Content);
    }

    [Fact]
    public async Task LongTextReplacementShowsBothChangedSpansWithoutDistantUnchangedText()
    {
        var prefix = new string('p', 500) + " The guild plays ";
        var suffix = " together. " + new string('s', 500);
        var oldText = "Mists of Pandaria";
        var newText = "World of Warcraft Forever " + new string('n', 250) + " with a final new detail";
        var fixture = GuildTextUpdate(prefix + oldText + suffix, prefix + newText + suffix);

        var reply = await fixture.Command.ExecuteAsync(1, "Correct the current game", Token);
        Assert.Contains(oldText, reply);
        Assert.Contains(newText, reply);
        Assert.DoesNotContain(prefix, reply);
        Assert.DoesNotContain(suffix, reply);
        Assert.Contains("The guild plays", reply);
    }

    [Fact]
    public async Task LongTextRemovalShowsWhatWasRemoved()
    {
        var prefix = new string('p', 500);
        var suffix = new string('s', 500);
        var removed = " Old claim to remove. ";
        var fixture = GuildTextUpdate(prefix + removed + suffix, prefix + suffix);

        var reply = await fixture.Command.ExecuteAsync(1, "Remove the old claim", Token);
        Assert.Contains(NotebookReview.DisplayText(removed), reply);
        Assert.DoesNotContain(prefix, reply);
        Assert.DoesNotContain(suffix, reply);
        Assert.Equal(prefix + suffix, fixture.Store.State.Entry!.Content);
    }

    [Theory]
    [InlineData("😀", "😁")]
    [InlineData("😀", "\U0001FA00")]
    public async Task ChangeBoundariesDoNotSplitEmojiThatShareASurrogate(string oldEmoji, string newEmoji)
    {
        var prefix = new string('p', 500);
        var suffix = new string('s', 500);
        var fixture = GuildTextUpdate(prefix + oldEmoji + suffix, prefix + newEmoji + suffix);

        var reply = await fixture.Command.ExecuteAsync(1, "Correct the emoji", Token);
        Assert.Contains(oldEmoji, reply);
        Assert.Contains(newEmoji, reply);
        // Strict encoding rejects any unpaired UTF-16 surrogate in the rendered excerpts.
        new UTF8Encoding(false, true).GetByteCount(reply!);
    }

    [Fact]
    public async Task ConfirmationStillShowsFullBeforeAndAfterWhileSavedSummaryFocusesOnTheChange()
    {
        var original = new string('p', 500);
        var addition = " A new final detail.";
        var fixture = GuildTextUpdate(original, original + addition);
        fixture.Planner.Plan = fixture.Planner.Plan with { RequiresConfirmation = true };

        var preview = await fixture.Command.ExecuteAsync(1, "Update the entry", Token);
        Assert.Contains(original, preview);
        Assert.Contains(NotebookReview.DisplayText(original + addition), preview);
        Assert.Equal(0, fixture.Store.Saves);
        var saved = await fixture.Command.ExecuteAsync(1, Confirmation(preview!), Token);
        Assert.Contains(NotebookReview.DisplayText(addition), saved);
        Assert.DoesNotContain(original, saved);
    }

    [Fact]
    public async Task NearbyContextAndLargeAdditionsRemainIntactThroughDisplayAndChunking()
    {
        var prefix = new string('p', 459) + "😀" + new string('p', 39);
        var suffix = new string('s', 39) + "😁" + new string('s', 459);
        var fixture = GuildTextUpdate(prefix + "old" + suffix, prefix + "new" + suffix);
        var changed = await fixture.Command.ExecuteAsync(1, "Correct the middle word", Token);
        Assert.Contains("😀", changed);
        Assert.Contains("😁", changed);
        new UTF8Encoding(false, true).GetByteCount(changed!);

        var addition = " New chapter: " + new string('a', 2500) + " Final detail.";
        fixture.Planner.Plan = fixture.Planner.Plan with { Changes = new() { Content = prefix + "new" + suffix + addition } };
        var appended = await fixture.Command.ExecuteAsync(1, "Append the new chapter", Token);
        Assert.Contains(NotebookReview.DisplayText(addition), appended);
        var chunks = MessageExtensions.SplitMessageContent(appended!);
        Assert.True(chunks.Count > 1);
        Assert.All(chunks, chunk => Assert.InRange(chunk.Length, 1, 2000));
        Assert.Equal(appended, string.Concat(chunks));
    }

    private static Fixture GuildTextUpdate(string before, string after) =>
        new(
            new("update", "History", "guild", new() { Content = after }, "Update history", false),
            new LoreEntrySnapshot("History", "guild") { Content = before }
        );

    private sealed class BlockingPlanner : ILoreEditPlanner
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public LoreEntrySnapshot? SecondInput { get; private set; }

        public async Task<LoreEditPlan> PlanAsync(
            string instruction,
            IReadOnlyList<LoreEntrySnapshot> entries,
            IReadOnlyList<LoreEditorTurn> conversation,
            CancellationToken cancellationToken
        )
        {
            if (++Calls == 1)
            {
                Started.SetResult();
                await Release.Task.WaitAsync(cancellationToken);
                return Update(new() { MainCharacter = "Paladin" });
            }
            SecondInput = Assert.Single(entries);
            return Update(new() { Biography = "Elevator joke. Another joke." });
        }
    }

    private static string Confirmation(string response) => Regex.Match(response, @"lore confirm [a-f0-9]{12}").Value;

    private static LoreEntrySnapshot Member() =>
        new("Grim", "member")
        {
            Pronouns = "he/him",
            Nationality = "UK",
            MainCharacter = "Shaman",
            Biography = "Elevator joke",
        };

    private static LoreEditPlan Create(LoreEditChanges changes) => new("create", "Grim", "member", changes, "Add Grim", false);

    private static LoreEditPlan Update(LoreEditChanges changes) => new("update", "Grim", "member", changes, "Update Grim", false);

    private sealed class Fixture
    {
        public MemoryStore Store { get; }
        public Planner Planner { get; }
        public Clock Clock { get; } = new();
        public LoreDirectMessageCommand Command { get; }

        public Fixture(LoreEditPlan plan, LoreEntrySnapshot? entry = null)
        {
            Store = new(entry);
            Planner = new() { Plan = plan };
            Command = new(
                Store,
                Planner,
                new(),
                Options.Create(new DirectMessageHandlerOptions { AdminUserId = 1 }),
                Clock,
                NullLogger<LoreDirectMessageCommand>.Instance
            );
        }
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Planner : ILoreEditPlanner
    {
        public required LoreEditPlan Plan { get; set; }
        public Action? OnPlan { get; set; }
        public int Calls { get; private set; }
        public IReadOnlyList<LoreEditorTurn>? Conversation { get; private set; }

        public Task<LoreEditPlan> PlanAsync(
            string instruction,
            IReadOnlyList<LoreEntrySnapshot> entries,
            IReadOnlyList<LoreEditorTurn> conversation,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            Conversation = conversation.ToArray();
            OnPlan?.Invoke();
            return Task.FromResult(Plan);
        }
    }

    private sealed class MemoryStore(LoreEntrySnapshot? entry) : IEditLoreStore
    {
        public LoreEditState State { get; private set; } = new(entry?.Name ?? "Grim", entry is not null, entry is null ? null : "initial", entry, []);
        public int Saves { get; private set; }
        public int Reads { get; private set; }
        public bool Reject { get; set; }

        public void ConcurrentEdit(LoreEntrySnapshot entry) => State = State with { Entry = entry, Revision = Guid.NewGuid().ToString("N") };

        public Task<IReadOnlyList<LoreEditState>> GetEntriesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Reads++;
            return Task.FromResult<IReadOnlyList<LoreEditState>>(State.Entry is null ? [] : [State]);
        }

        public Task<LoreEditState> GetEntryAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Reads++;
            return Task.FromResult(
                State.Exists && State.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ? State : new(name, false, null, null, [])
            );
        }

        public Task<bool> TrySaveAsync(LoreEditState expected, LoreRevision revision, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Reject || State.Revision != expected.Revision || State.Exists != expected.Exists)
                return Task.FromResult(false);
            Saves++;
            State = new(expected.Name, true, revision.Id, revision.After, State.History.Append(revision).ToArray());
            return Task.FromResult(true);
        }
    }
}
