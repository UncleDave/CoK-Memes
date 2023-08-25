using MediatR;

namespace ChampionsOfKhazad.Bot.HardcoreStats.CharacterDeaths;

internal class CharacterDeathReportedHandler : INotificationHandler<CharacterDeathReported>
{
    private readonly IRecordCharacterDeaths _deathRecorder;

    public CharacterDeathReportedHandler(IRecordCharacterDeaths deathRecorder)
    {
        _deathRecorder = deathRecorder;
    }

    public Task Handle(CharacterDeathReported notification, CancellationToken cancellationToken) =>
        _deathRecorder.RecordCharacterDeathAsync(notification.CharacterDeath);
}
