using System.Net;
using ChampionsOfKhazad.Bot.BlizzardProbe;

namespace ChampionsOfKhazad.Bot.Tests;

public class BlizzardProbeGuildRoutingTests
{
    [Fact]
    public async Task GuildProbeDefaultsToTheRealmlessForeverRoute()
    {
        var paths = new List<string>();
        using var http = new HttpClient(
            new Handler(request =>
            {
                paths.Add(request.RequestUri!.AbsolutePath);
                return new HttpResponseMessage(request.RequestUri.Host == "oauth.battle.net" ? HttpStatusCode.OK : HttpStatusCode.NotFound)
                {
                    Content = new StringContent("""{"access_token":"test-token"}"""),
                };
            })
        );
        using var output = new StringWriter();
        var options = ProbeOptions.Parse(["--guild", "crossroads-mega-chippy"]);
        Assert.Equal(0, await new BlizzardApiProbe(http, output).RunAsync(options, "id", "secret", TestContext.Current.CancellationToken));
        Assert.Contains("/data/wow/guild/crossroads-mega-chippy", paths);
        Assert.Contains("/data/wow/guild/crossroads-mega-chippy/roster", paths);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(send(request));
    }
}
