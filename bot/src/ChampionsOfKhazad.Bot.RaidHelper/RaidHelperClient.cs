using System.Net.Http.Json;

namespace ChampionsOfKhazad.Bot.RaidHelper;

public class RaidHelperClient(HttpClient httpClient)
{
    public async Task<EventsResponse> GetEventsAsync(ulong guildId)
    {
        var response = await httpClient.GetFromJsonAsync<EventsResponse>($"v3/servers/{guildId}/events");

        return response ?? throw new ApplicationException("Failed to get events");
    }

    public Task CreateEventAsync(ulong guildId, ulong channelId, CreateEventRequest request) =>
        httpClient.PostAsJsonAsync($"v2/servers/{guildId}/channels/{channelId}/event", request);
}
