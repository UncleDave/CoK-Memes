using Microsoft.AspNetCore.Authorization;

namespace ChampionsOfKhazad.Bot.Portal;

public record DiscordGuildRequirement(ulong GuildId) : IAuthorizationRequirement;
