﻿using System.ComponentModel.DataAnnotations;

namespace ChampionsOfKhazad.Bot;

public class BotOptions
{
    public const string Key = "Bot";

    [Required]
    public required string Token { get; set; }

    [Required]
    public required ulong GuildId { get; set; }
}
