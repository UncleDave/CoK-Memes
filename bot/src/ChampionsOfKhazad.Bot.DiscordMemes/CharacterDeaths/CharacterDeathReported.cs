using MediatR;

namespace ChampionsOfKhazad.Bot.DiscordMemes.CharacterDeaths;

public record CharacterDeathReported(CharacterDeath CharacterDeath) : INotification;
