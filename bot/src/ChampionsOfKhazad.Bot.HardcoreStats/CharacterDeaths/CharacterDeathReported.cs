using MediatR;

namespace ChampionsOfKhazad.Bot.HardcoreStats.CharacterDeaths;

public record CharacterDeathReported(CharacterDeath CharacterDeath) : INotification;
