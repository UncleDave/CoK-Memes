namespace ChampionsOfKhazad.Bot.Roster;

public sealed class GuildRosterService(
    GuildRosterOptions options,
    IGuildRosterSource source,
    IRosterStore store,
    IRosterNoticeStore notices,
    TimeProvider clock
) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    public string ScopeId => $"{options.GuildId}:{source.Id}";
    public bool Enabled => options.Enabled;
    public bool IsMock => source.IsMock;
    public bool NotificationsEnabled => options.Enabled && options.MilestoneChannelId is > 0 && (!source.IsMock || options.AllowMockNotifications);

    public async Task RefreshAsync(CancellationToken cancellationToken, bool force = false)
    {
        if (!options.Enabled)
            return;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var previous = await store.GetAsync(ScopeId, cancellationToken) ?? new RosterState { Id = ScopeId };
            var now = clock.GetUtcNow().UtcDateTime;
            if (!force && previous.CheckedAtUtc is { } last && now - last < TimeSpan.FromSeconds(options.RefreshSeconds))
                return;
            RosterState current;
            try
            {
                var observation = RosterValidator.Validate(await source.GetRosterAsync(cancellationToken), now);
                if (previous.SourceGuildId is not null && observation.GuildId is not null && previous.SourceGuildId != observation.GuildId)
                    throw new InvalidDataException("The roster response belongs to a different source guild.");
                if (previous.ObservedAtUtc is { } observed && observation.ObservedAtUtc < observed)
                    throw new InvalidDataException("The roster response is older than the accepted observation.");
                var members = new List<RosterCharacter>();
                var oldMembers = previous.Characters.ToDictionary(member => member.LookupKey, StringComparer.Ordinal);
                foreach (var incoming in observation.Characters)
                {
                    var old = oldMembers.GetValueOrDefault(incoming.LookupKey);
                    var member = incoming with
                    {
                        IdentityInstance = incoming.Id is null ? old?.IdentityInstance ?? Guid.NewGuid().ToString("N") : null,
                        Professions = old?.Professions,
                        ProfessionsCheckedAtUtc = old?.ProfessionsCheckedAtUtc,
                        ProfessionRefreshFailed = old?.ProfessionRefreshFailed ?? false,
                    };
                    if (!source.HasSeparateProfessionRequests && incoming.Professions is { } combined)
                        member = member with
                        {
                            Professions = Merge(old?.Professions, RosterValidator.Validate(combined, observation.ObservedAtUtc, now)),
                            ProfessionsCheckedAtUtc = now,
                            ProfessionRefreshFailed = false,
                        };
                    else if (
                        source.HasSeparateProfessionRequests
                        && (
                            member.ProfessionsCheckedAtUtc is not { } checkedAt
                            || now - checkedAt >= TimeSpan.FromSeconds(options.ProfessionRefreshSeconds)
                        )
                    )
                    {
                        try
                        {
                            var separate = await source.GetProfessionsAsync(incoming, cancellationToken);
                            member = member with
                            {
                                Professions = separate is null
                                    ? member.Professions
                                    : Merge(member.Professions, RosterValidator.Validate(separate, now, now)),
                                ProfessionsCheckedAtUtc = now,
                                ProfessionRefreshFailed = separate is null,
                            };
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch
                        {
                            member = member with { ProfessionsCheckedAtUtc = now, ProfessionRefreshFailed = true };
                        }
                    }
                    members.Add(member);
                }
                current = previous with
                {
                    SourceId = source.Id,
                    IsMock = source.IsMock,
                    GuildName = observation.GuildName,
                    SourceGuildId = observation.GuildId ?? previous.SourceGuildId,
                    ObservedAtUtc = observation.ObservedAtUtc,
                    CheckedAtUtc = now,
                    LastSucceededAtUtc = now,
                    LastRefreshFailed = false,
                    Characters = members.ToArray(),
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                await store.TrySaveAsync(previous with { CheckedAtUtc = now, LastRefreshFailed = true }, cancellationToken);
                throw;
            }
            var changes = RosterMilestones.Compare(previous, current, options, now, NotificationsEnabled);
            current = current with { Progress = changes.Progress };
            if (!await store.TrySaveAsync(current, cancellationToken))
                return;
            // Best effort by design: no transaction spans snapshot/progress and notice writes.
            await notices.RemoveDepartedPendingAsync(ScopeId, current.Characters.Select(member => member.Key).ToArray(), cancellationToken);
            foreach (var notice in changes.Notices)
                await notices.AddPendingAsync(notice, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeliverAsync(IRosterNotificationSender sender, CancellationToken cancellationToken)
    {
        if (!NotificationsEnabled)
            return;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await store.GetAsync(ScopeId, cancellationToken);
            if (state?.ObservedAtUtc is null)
                return;
            var members = state.Characters.Select(member => member.Key).ToHashSet(StringComparer.Ordinal);
            await notices.RemoveDepartedPendingAsync(ScopeId, members.ToArray(), cancellationToken);
            var pending = await notices.GetPendingAsync(ScopeId, cancellationToken);
            foreach (var batch in RosterText.NoticeBatches(pending.Where(item => members.Contains(item.CharacterKey))))
            {
                var messageId = await sender.SendAsync(options.GuildId, options.MilestoneChannelId!.Value, batch.Text, cancellationToken);
                foreach (var notice in batch.Notices)
                    await notices.MarkSentAsync(notice.Id, clock.GetUtcNow().UtcDateTime, messageId, cancellationToken);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private static ProfessionObservation Merge(ProfessionObservation? old, ProfessionObservation incoming)
    {
        if (old is null)
            return incoming;
        if (incoming.ObservedAtUtc < old.ObservedAtUtc)
            return old;
        var skills = incoming.Complete
            ? new Dictionary<string, ProfessionSkill>(StringComparer.Ordinal)
            : old.Skills.ToDictionary(skill => skill.TrackKey, StringComparer.Ordinal);
        foreach (var skill in incoming.Skills)
        {
            var previous = old.Skills.FirstOrDefault(item => item.TrackKey == skill.TrackKey);
            skills[skill.TrackKey] = previous is not null && previous.ObservedAtUtc > skill.ObservedAtUtc ? previous : skill;
        }
        return incoming with { Skills = skills.Values.ToArray() };
    }

    public void Dispose() => _gate.Dispose();
}
