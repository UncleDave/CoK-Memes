using MediatR;

namespace ChampionsOfKhazad.Bot.DiscordMemes.CharacterDeaths;

internal class CharacterDeathReportedHandler(IStoreCharacterDeaths deathRecorder) : INotificationHandler<CharacterDeathReported>
{
    public Task Handle(CharacterDeathReported notification, CancellationToken cancellationToken) =>
        deathRecorder.InsertCharacterDeathAsync(notification.CharacterDeath);
}
