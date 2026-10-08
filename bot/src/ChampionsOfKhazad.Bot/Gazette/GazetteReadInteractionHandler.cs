using ChampionsOfKhazad.Bot.GenAi;
using Discord;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot;

public sealed class GazetteReadInteractionHandler(
    IGazettePublishedEditionStore store,
    IGazetteReaderAccess access,
    ILogger<GazetteReadInteractionHandler> logger
) : INotificationHandler<GazetteReadRequested>
{
    public async Task Handle(GazetteReadRequested notification, CancellationToken cancellationToken)
    {
        var interaction = notification.Interaction;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            if (!GazetteReadButton.TryParse(interaction.Data.CustomId, out var publicationId) || !access.CanRead(interaction))
            {
                await UnavailableAsync(interaction, timeout.Token);
                return;
            }
            var edition = await store.GetAsync(publicationId, timeout.Token);
            if (
                edition is null
                || !MatchesPublication(edition, interaction.GuildId, interaction.ChannelId, interaction.Message.Id)
                || !access.CanRead(interaction)
            )
            {
                await UnavailableAsync(interaction, timeout.Token);
                return;
            }
            await interaction.FollowupAsync(
                embed: new EmbedBuilder().WithTitle("Readable edition & sources").WithDescription(edition.Text).Build(),
                ephemeral: true,
                allowedMentions: AllowedMentions.None,
                options: new RequestOptions { CancelToken = timeout.Token }
            );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning("Gazette private reader failed with {ExceptionType}", exception.GetType().Name);
            await UnavailableAsync(interaction, cancellationToken);
        }
    }

    internal static bool MatchesPublication(GazettePublishedEdition edition, ulong? guildId, ulong? channelId, ulong messageId) =>
        edition.GuildId == guildId
        && edition.ChannelId == channelId
        && (edition.MessageId is null || edition.MessageId == messageId)
        && !string.IsNullOrWhiteSpace(edition.Text)
        && edition.Text.Length <= 4000;

    private static Task UnavailableAsync(IComponentInteraction interaction, CancellationToken cancellationToken) =>
        interaction.FollowupAsync(
            "This edition's readable text is unavailable right now. Try again shortly.",
            ephemeral: true,
            allowedMentions: AllowedMentions.None,
            options: new RequestOptions { CancelToken = cancellationToken }
        );
}
