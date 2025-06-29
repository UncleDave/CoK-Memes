using System.Text.Json.Serialization;

namespace ChampionsOfKhazad.Bot.RaidHelper;

public class CreateEventRequestAdvancedSettings
{
    public ushort? Duration { get; set; }

    [JsonPropertyName("font_style")]
    public ushort? FontStyle { get; set; }

    [JsonPropertyName("tentative_emote")]
    public string? TentativeEmote { get; set; }

    public string? Mentions { get; set; }
}

public class CreateEventRequest
{
    public required string LeaderId { get; init; }
    public string? TemplateId { get; set; }
    public string? Date { get; set; }
    public string? Time { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public CreateEventRequestAdvancedSettings? AdvancedSettings { get; set; }
}
