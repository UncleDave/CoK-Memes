using System.Net;
using System.Text;
using ChampionsOfKhazad.Bot.BlizzardProbe;

namespace ChampionsOfKhazad.Bot.Tests;

public class BlizzardApiProbeTests
{
    [Fact]
    public async Task UsesScopedCredentialsAndSummarizesWithoutDumpingBodies()
    {
        var requests = new List<(Uri Uri, string? Scheme, string? Parameter, string? Body)>();
        using var handler = new ProbeHandler(async request =>
        {
            requests.Add(
                (
                    request.RequestUri!,
                    request.Headers.Authorization?.Scheme,
                    request.Headers.Authorization?.Parameter,
                    request.Content is null ? null : await request.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
                )
            );
            if (request.RequestUri!.Host == "oauth.battle.net")
                return Json(HttpStatusCode.OK, """{"access_token":"private-token","ignored":"private-secret"}""");
            var result = Json(HttpStatusCode.OK, """{"realms":[{"name":"PRIVATE-NAME"}],"unexpected":"private-token","name":"PRIVATE-NAME"}""");
            result.Headers.TryAddWithoutValidation("ETag", "private-secret private-token\u001b[31m");
            result.Headers.TryAddWithoutValidation("Cache-Control", "max-age=86400");
            return result;
        });
        using var http = new HttpClient(handler);
        using var output = new StringWriter();
        var code = await new BlizzardApiProbe(http, output).RunAsync(
            ProbeOptions.Parse([]),
            "public-client",
            "private-secret",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(0, code);
        Assert.Equal(8, requests.Count);
        Assert.Equal(new Uri("https://oauth.battle.net/token"), requests[0].Uri);
        Assert.Equal("Basic", requests[0].Scheme);
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("public-client:private-secret")), requests[0].Parameter);
        Assert.Equal("grant_type=client_credentials", requests[0].Body);
        Assert.All(
            requests.Skip(1),
            request =>
            {
                Assert.Equal("eu.api.blizzard.com", request.Uri.Host);
                Assert.Equal("https", request.Uri.Scheme);
                Assert.Equal("Bearer", request.Scheme);
                Assert.Equal("private-token", request.Parameter);
                Assert.Null(request.Body);
                Assert.DoesNotContain("private-token", request.Uri.ToString());
                Assert.DoesNotContain("private-secret", request.Uri.ToString());
            }
        );
        Assert.Contains(requests, request => request.Uri.Query.Contains("dynamic-probe-invalid-eu"));
        Assert.Contains(requests, request => request.Uri.Query.Contains("static-1.60.1_70399-forever-eu"));
        Assert.Null(http.DefaultRequestHeaders.Authorization);
        Assert.Contains("realms count=1; name present", output.ToString());
        Assert.Contains("max-age=86400", output.ToString());
        Assert.Contains("[redacted]", output.ToString());
        Assert.DoesNotContain("PRIVATE-NAME", output.ToString());
        Assert.DoesNotContain("private-secret", output.ToString());
        Assert.DoesNotContain("private-token", output.ToString());
        Assert.DoesNotContain('\u001b', output.ToString());
        Assert.DoesNotContain(requests[0].Parameter!, output.ToString());
    }

    [Fact]
    public async Task OptionalRoutesUseProfileNamespaceWithoutRequestingUserAccountData()
    {
        var requests = new List<Uri>();
        using var handler = new ProbeHandler(request =>
        {
            requests.Add(request.RequestUri!);
            return Task.FromResult(
                requests.Count == 1
                    ? Json(HttpStatusCode.OK, """{"access_token":"token"}""")
                    : Json(HttpStatusCode.NotFound, """{"error":"not a roster"}""")
            );
        });
        using var http = new HttpClient(handler);
        using var output = new StringWriter();
        var options = ProbeOptions.Parse([
            "--region",
            "us",
            "--static-build",
            "1.60.2_12345",
            "--character",
            "élodie-firebrand",
            "--realm",
            "camwow-us1-realm-1-pve",
            "--guild",
            "test-guild",
        ]);
        Assert.Equal(0, await new BlizzardApiProbe(http, output).RunAsync(options, "id", "secret", TestContext.Current.CancellationToken));
        Assert.Equal(12, requests.Count);
        Assert.Contains(
            requests,
            uri => uri.AbsolutePath == "/profile/wow/character/%C3%A9lodie-firebrand/professions" && uri.Query.Contains("profile-forever-us")
        );
        Assert.Contains(
            requests,
            uri => uri.AbsolutePath == "/data/wow/guild/camwow-us1-realm-1-pve/test-guild/roster" && uri.Query.Contains("profile-forever-us")
        );
        Assert.DoesNotContain(requests, uri => uri.AbsolutePath.StartsWith("/profile/user/", StringComparison.Ordinal));
        Assert.DoesNotContain("not a roster", output.ToString());
    }

    [Theory]
    [InlineData(401)]
    [InlineData(429)]
    [InlineData(503)]
    [InlineData(302)]
    public async Task StopsWithoutRetryingOnUpstreamFailure(int status)
    {
        var calls = 0;
        using var handler = new ProbeHandler(_ =>
            Task.FromResult(
                ++calls == 1 ? Json(HttpStatusCode.OK, """{"access_token":"token"}""") : Json((HttpStatusCode)status, "secret response body")
            )
        );
        using var http = new HttpClient(handler);
        using var output = new StringWriter();
        Assert.Equal(
            1,
            await new BlizzardApiProbe(http, output).RunAsync(ProbeOptions.Parse([]), "id", "secret", TestContext.Current.CancellationToken)
        );
        Assert.Equal(2, calls);
        Assert.DoesNotContain("secret response body", output.ToString());
    }

    [Fact]
    public async Task AuthenticationFailureDoesNotPrintBodyOrMakeApiCalls()
    {
        var calls = 0;
        using var handler = new ProbeHandler(_ =>
        {
            calls++;
            return Task.FromResult(Json(HttpStatusCode.Unauthorized, "secret credentials echoed by server"));
        });
        using var http = new HttpClient(handler);
        using var output = new StringWriter();
        Assert.Equal(
            1,
            await new BlizzardApiProbe(http, output).RunAsync(ProbeOptions.Parse([]), "id", "secret", TestContext.Current.CancellationToken)
        );
        Assert.Equal(1, calls);
        Assert.Equal($"OAuth: HTTP 401{Environment.NewLine}", output.ToString());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"access_token\":null}")]
    [InlineData("{\"access_token\":123}")]
    [InlineData("{\"access_token\":\" \"}")]
    public async Task MissingTokenStopsBeforeApiRequests(string body)
    {
        var calls = 0;
        using var handler = new ProbeHandler(_ =>
        {
            calls++;
            return Task.FromResult(Json(HttpStatusCode.OK, body));
        });
        using var http = new HttpClient(handler);
        using var output = new StringWriter();
        Assert.Equal(
            1,
            await new BlizzardApiProbe(http, output).RunAsync(ProbeOptions.Parse([]), "id", "secret", TestContext.Current.CancellationToken)
        );
        Assert.Equal(1, calls);
        Assert.Contains("did not contain an access token", output.ToString());
    }

    [Fact]
    public async Task InvalidAndOversizedSuccessBodiesAreNotDumped()
    {
        var calls = 0;
        using var handler = new ProbeHandler(_ =>
            Task.FromResult(
                ++calls == 1
                    ? Json(HttpStatusCode.OK, """{"access_token":"token"}""")
                    : Json(HttpStatusCode.OK, calls == 2 ? "not-json PRIVATE" : new string('x', 2 * 1024 * 1024 + 1))
            )
        );
        using var http = new HttpClient(handler);
        using var output = new StringWriter();
        Assert.Equal(
            0,
            await new BlizzardApiProbe(http, output).RunAsync(ProbeOptions.Parse([]), "id", "secret", TestContext.Current.CancellationToken)
        );
        Assert.Contains("body suppressed", output.ToString());
        Assert.Contains("size limit", output.ToString());
        Assert.DoesNotContain("PRIVATE", output.ToString());
        Assert.DoesNotContain(new string('x', 100), output.ToString());
    }

    [Theory]
    [InlineData("--region", "evil.example")]
    [InlineData("--character", "../../user/wow")]
    [InlineData("--character", "name?access_token=secret")]
    [InlineData("--realm", "valid-slug")]
    [InlineData("--static-build", "1.60.1_70399&namespace=other")]
    [InlineData("--client-secret", "must-not-be-echoed")]
    public void RejectsInvalidOptionsWithoutEchoingValues(string key, string value)
    {
        var exception = Assert.Throws<ArgumentException>(() => ProbeOptions.Parse([key, value]));
        Assert.DoesNotContain(value, exception.Message);
    }

    [Fact]
    public void RejectsRepeatedAndIncompleteOptions()
    {
        Assert.Throws<ArgumentException>(() => ProbeOptions.Parse(["--region"]));
        Assert.Throws<ArgumentException>(() => ProbeOptions.Parse(["--region", "eu", "--region", "us"]));
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class ProbeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }
}
