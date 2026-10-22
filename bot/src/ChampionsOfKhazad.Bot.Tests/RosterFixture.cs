using ChampionsOfKhazad.Bot.Roster;

namespace ChampionsOfKhazad.Bot.Tests;

internal sealed class RosterFixture : IDisposable
{
    public GuildRosterOptions Options { get; } =
        new()
        {
            Enabled = true,
            GuildId = 50,
            AllowMockNotifications = true,
            MilestoneChannelId = 100,
        };
    public RosterClock Clock { get; } = new();
    public MemoryRosterStore Store { get; } = new();
    public MemoryRosterNotices Notices { get; } = new();
    public RosterSource Source { get; } = new();
    public GuildRosterService Service { get; }
    public GuildRosterQueries Queries { get; }
    public CancellationToken Token => TestContext.Current.CancellationToken;

    public RosterFixture()
    {
        Service = new(Options, Source, Store, Notices, Clock);
        Queries = new(Options, Service, Store);
        SetMembers(Character());
    }

    public void SetMembers(params RosterCharacter[] characters) =>
        Source.Observation = new()
        {
            GuildName = "Mock Guild",
            Complete = true,
            ObservedAtUtc = Clock.GetUtcNow().UtcDateTime,
            Characters = characters,
        };

    public Task Refresh() => Service.RefreshAsync(Token, force: true);

    public void Advance(int seconds = 60) => Clock.Now = Clock.Now.AddSeconds(seconds);

    public static RosterCharacter Character(
        string id = "1",
        int level = 19,
        string first = "Borin",
        string last = "Ironhide",
        ProfessionObservation? professions = null
    ) =>
        new()
        {
            Id = id,
            FirstName = first,
            LastName = last,
            ClassName = "Warrior",
            Level = level,
            Professions = professions,
        };

    public static ProfessionObservation Professions(
        int? rank = 74,
        bool complete = true,
        string id = "171",
        string name = "Alchemy",
        string tier = ""
    ) =>
        new()
        {
            Complete = complete,
            Skills =
            [
                new()
                {
                    Id = id,
                    Name = name,
                    Skill = rank,
                    MaxSkill = 300,
                    Tier = tier,
                },
            ],
        };

    public void Dispose() => Service.Dispose();

    internal sealed class RosterClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow.AddSeconds(1);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    internal sealed class MemoryRosterStore : IRosterStore
    {
        public Dictionary<string, RosterState> States { get; } = [];
        public int Reads { get; private set; }
        public bool FailReads { get; set; }
        public bool Conflict { get; set; }

        public Task<RosterState?> GetAsync(string id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Reads++;
            if (FailReads)
                throw new InvalidOperationException("Private database detail");
            return Task.FromResult(States.GetValueOrDefault(id));
        }

        public Task<bool> TrySaveAsync(RosterState state, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Conflict || state.Revision != (States.GetValueOrDefault(state.Id)?.Revision ?? 0))
                return Task.FromResult(false);
            States[state.Id] = state with { Revision = state.Revision + 1 };
            return Task.FromResult(true);
        }
    }

    internal sealed class MemoryRosterNotices : IRosterNoticeStore
    {
        public Dictionary<string, RosterNotice> Items { get; } = [];
        public List<string> Steps { get; } = [];

        public Task AddPendingAsync(RosterNotice notice, CancellationToken cancellationToken)
        {
            Steps.Add("pending");
            Items.TryAdd(notice.Id, notice);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RosterNotice>> GetPendingAsync(string scopeId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RosterNotice>>(
                Items.Values.Where(item => item.ScopeId == scopeId && item.SentAtUtc is null).OrderBy(item => item.Id).ToArray()
            );

        public Task MarkSentAsync(string id, DateTime sentAtUtc, ulong messageId, CancellationToken cancellationToken)
        {
            Steps.Add("sent");
            Items[id] = Items[id] with { SentAtUtc = sentAtUtc, DiscordMessageId = messageId };
            return Task.CompletedTask;
        }

        public Task RemoveDepartedPendingAsync(string scopeId, string[] activeCharacterKeys, CancellationToken cancellationToken)
        {
            foreach (
                var id in Items
                    .Values.Where(item => item.ScopeId == scopeId && item.SentAtUtc is null && !activeCharacterKeys.Contains(item.CharacterKey))
                    .Select(item => item.Id)
                    .ToArray()
            )
                Items.Remove(id);
            return Task.CompletedTask;
        }
    }

    internal sealed class RosterSource : IGuildRosterSource
    {
        public string Id => "mock-test";
        public bool IsMock => true;
        public bool HasSeparateProfessionRequests { get; set; }
        public RosterObservation Observation { get; set; } = null!;
        public Dictionary<string, ProfessionObservation?> Professions { get; } = [];
        public int RosterReads { get; private set; }
        public int ProfessionReads { get; private set; }
        public Exception? Failure { get; set; }
        public Exception? ProfessionFailure { get; set; }
        public Func<Task>? BeforeRead { get; set; }

        public async Task<RosterObservation> GetRosterAsync(CancellationToken cancellationToken)
        {
            RosterReads++;
            cancellationToken.ThrowIfCancellationRequested();
            if (BeforeRead is not null)
                await BeforeRead();
            if (Failure is not null)
                throw Failure;
            return Observation;
        }

        public Task<ProfessionObservation?> GetProfessionsAsync(RosterCharacter character, CancellationToken cancellationToken)
        {
            ProfessionReads++;
            cancellationToken.ThrowIfCancellationRequested();
            if (ProfessionFailure is not null)
                throw ProfessionFailure;
            return Task.FromResult(Professions.GetValueOrDefault(character.Key));
        }
    }
}
