using MediatR;

namespace ChampionsOfKhazad.Bot.HardcoreStats.CharacterDeaths;

internal class CharacterDeathReportedHandler(IRecordCharacterDeaths deathRecorder) : INotificationHandler<CharacterDeathReported>
{
    public Task Handle(CharacterDeathReported notification, CancellationToken cancellationToken) =>
        deathRecorder.RecordCharacterDeathAsync(notification.CharacterDeath);
}
