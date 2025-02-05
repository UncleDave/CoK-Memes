using MediatR;

namespace ChampionsOfKhazad.Bot.DiscordMemes.CharacterDeaths;

internal class CharacterDeathReportedHandler(IStoreCharacterDeaths characterDeathStore) : INotificationHandler<CharacterDeathReported>
{
    public Task Handle(CharacterDeathReported notification, CancellationToken cancellationToken) =>
        characterDeathStore.InsertCharacterDeathAsync(notification.CharacterDeath);
}
