namespace ChampionsOfKhazad.Bot.GenAi;

public interface IGazettePublishedEditionStore
{
    Task<GazettePublishedEdition?> GetAsync(string id, CancellationToken cancellationToken);
    Task<IReadOnlyList<GazettePublishedEdition>> GetRecentAsync(
        ulong guildId,
        ulong channelId,
        DateTime sinceUtc,
        DateTime untilUtc,
        int maximumEditions,
        CancellationToken cancellationToken
    );
    Task SaveAsync(GazettePublishedEdition edition, CancellationToken cancellationToken);
    Task ConfirmMessageAsync(string id, ulong messageId, CancellationToken cancellationToken);
}
