using ChampionsOfKhazad.Bot.GenAi;
using Discord;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

internal class DiscordNotebookReviewer(BotContext context, IOptions<DirectMessageHandlerOptions> options, ILogger<DiscordNotebookReviewer> logger)
    : INotebookReviewer
{
    public async Task<bool> NotifyAsync(NotebookNote note, CancellationToken cancellationToken)
    {
        try
        {
            var requestOptions = new RequestOptions { CancelToken = cancellationToken };
            var admin = await context.Client.GetUserAsync(options.Value.AdminUserId, options: requestOptions);
            if (admin is null)
                return false;
            await admin.SendMessageAsync(
                note.Origin == NotebookOrigin.Background
                    ? "The Lorekeeper noticed a new notebook entry during background observation. Established lore is unchanged."
                    : "The Lorekeeper chose a new notebook entry. Established lore is unchanged.",
                embed: NotebookReview.CreateEmbed(note),
                allowedMentions: AllowedMentions.None,
                options: requestOptions
            );
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Notebook review DM failed for note {NoteId}", note.Id);
            return false;
        }
    }
}
