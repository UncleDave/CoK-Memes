using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using ChampionsOfKhazad.Bot.Roster;
using ChampionsOfKhazad.Bot.Roster.Blizzard;

namespace ChampionsOfKhazad.Bot.Tests;

public class BlizzardGuildRosterSourceTests
{
    private const string Roster = """
        {"guild":{"id":42,"name":"Crossroads Mega Chippy"},"members":[
          {"character":{"id":1001,"name":"Borin","surname":"Ironhide","level":19,"playable_class":{"id":1},
            "key":{"href":"https://eu.api.blizzard.com/profile/wow/character/borin-ironhide?namespace=profile-forever-eu"}}}
        ]}
        """;
    private const string Professions = """
        {"character":{"id":1001},"primaries":[{"profession":{"id":164,"name":"Blacksmithing"},
          "tiers":[{"tier":{"id":1},"skill_points":74,"max_skill_points":150}]}],
          "secondaries":[{"profession":{"id":185,"name":"Cooking"},"skill_points":0,"max_skill_points":75}]}
        """;

    [Fact]
    public async Task LiveSourceUsesRealmlessRoutesOAuthSurnameAndStaticClassNames()
    {
        using var fixture = new Fixture();
        var roster = await fixture.Source.GetRosterAsync(fixture.Token);
        var character = Assert.Single(roster.Characters);
        Assert.False(fixture.Source.IsMock);
        Assert.True(roster.Complete);
        Assert.Equal("42", roster.GuildId);
        Assert.Equal("Borin Ironhide", character.FullName);
        Assert.Equal("Warrior", character.ClassName);
        Assert.Equal("1001", character.Id);
        Assert.Equal(fixture.Clock.Now.UtcDateTime, roster.ObservedAtUtc);
        var professions = await fixture.Source.GetProfessionsAsync(character, fixture.Token);
        Assert.True(professions!.Complete);
        Assert.Equal(["Blacksmithing", "Cooking"], professions.Skills.Select(skill => skill.Name).ToArray());
        Assert.Equal(0, professions.Skills[1].Skill);
        Assert.Equal("1", professions.Skills[0].Tier);
        Assert.Equal(1, fixture.OAuthCalls);
        Assert.Contains(
            fixture.Requests,
            request => request.Path == "/data/wow/guild/crossroads-mega-chippy/roster" && request.Query.Contains("profile-forever-eu")
        );
        Assert.Contains(fixture.Requests, request => request.Path == "/data/wow/playable-class/index" && request.Query.Contains("static-forever-eu"));
        Assert.Contains(fixture.Requests, request => request.Path == "/profile/wow/character/borin-ironhide/professions");
        Assert.All(
            fixture.Requests.Where(request => request.Path != "/token"),
            request =>
            {
                Assert.Equal("eu.api.blizzard.com", request.Host);
                Assert.Equal("Bearer", request.Scheme);
                Assert.DoesNotContain("private-token", request.Query);
            }
        );
    }

    [Fact]
    public async Task SuccessfulLiveLevelsSeedSilentlyThenQueueRealNoticesAndFailuresKeepTheRoster()
    {
        using var fixture = new Fixture();
        using var memory = new RosterFixture();
        using var service = new GuildRosterService(memory.Options, fixture.Source, memory.Store, memory.Notices, fixture.Clock);
        await service.RefreshAsync(fixture.Token, force: true);
        Assert.Empty(memory.Notices.Items);
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(601);
        fixture.RosterJson = Roster.Replace("\"level\":19", "\"level\":20", StringComparison.Ordinal);
        fixture.ProfessionJson = Professions.Replace("\"skill_points\":74", "\"skill_points\":75", StringComparison.Ordinal);
        await service.RefreshAsync(fixture.Token, force: true);
        Assert.Equal(2, memory.Notices.Items.Count);
        Assert.All(memory.Notices.Items.Values, notice => Assert.False(notice.IsMock));
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(61);
        fixture.ResourceStatus = HttpStatusCode.NotFound;
        await Assert.ThrowsAsync<RosterSourceUnavailableException>(() => service.RefreshAsync(fixture.Token, force: true));
        var retained = memory.Store.States[service.ScopeId];
        Assert.Equal(20, Assert.Single(retained.Characters).Level);
        Assert.True(retained.LastRefreshFailed);
        Assert.Equal(2, memory.Notices.Items.Count);
    }

    [Theory]
    [InlineData("surname")]
    [InlineData("duplicate")]
    [InlineData("different-guild")]
    [InlineData("pagination")]
    [InlineData("count-mismatch")]
    [InlineData("foreign-link")]
    [InlineData("bad-level")]
    public async Task RejectsPartialOrUnexpectedMembershipInsteadOfPublishingAnEmptyOrMixedRoster(string change)
    {
        using var fixture = new Fixture();
        var root = JsonNode.Parse(Roster)!;
        var character = root["members"]![0]!["character"]!;
        switch (change)
        {
            case "surname":
                character.AsObject().Remove("surname");
                break;
            case "duplicate":
                root["members"]!.AsArray().Add(root["members"]![0]!.DeepClone());
                break;
            case "different-guild":
                root["guild"]!["name"] = "Other Guild";
                break;
            case "pagination":
                root["_links"] = JsonNode.Parse("""{"next":{"href":"https://example.test/next"}}""");
                break;
            case "count-mismatch":
                root["member_count"] = 2;
                break;
            case "foreign-link":
                character["key"]!["href"] = "https://example.test/profile/wow/character/borin-ironhide";
                break;
            case "bad-level":
                character["level"] = 0;
                break;
        }
        fixture.RosterJson = root.ToJsonString();
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Source.GetRosterAsync(fixture.Token));
        Assert.DoesNotContain("private-", error.ToString());
        Assert.DoesNotContain(fixture.Requests, request => request.Host == "example.test");
    }

    [Theory]
    [InlineData("{}", null)]
    [InlineData("{\"primaries\":[]}", false)]
    [InlineData("{\"primaries\":[],\"secondaries\":[]}", true)]
    public async Task MissingPartialAndExplicitlyEmptyProfessionsRemainDistinct(string body, bool? complete)
    {
        using var fixture = new Fixture { ProfessionJson = body };
        var member = Assert.Single((await fixture.Source.GetRosterAsync(fixture.Token)).Characters);
        var observation = await fixture.Source.GetProfessionsAsync(member, fixture.Token);
        if (complete is null)
            Assert.Null(observation);
        else
        {
            Assert.Equal(complete, observation!.Complete);
            Assert.Empty(observation.Skills);
        }
    }

    [Fact]
    public async Task MissingProfessionEndpointIsUnknownAndWrongCharacterCannotSupplySkills()
    {
        using var fixture = new Fixture();
        var member = Assert.Single((await fixture.Source.GetRosterAsync(fixture.Token)).Characters);
        fixture.ProfessionStatus = HttpStatusCode.NotFound;
        Assert.Null(await fixture.Source.GetProfessionsAsync(member, fixture.Token));
        fixture.ProfessionStatus = HttpStatusCode.OK;
        fixture.ProfessionJson = Professions.Replace("1001", "9999", StringComparison.Ordinal);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Source.GetProfessionsAsync(member, fixture.Token));
    }

    [Fact]
    public async Task HonorsMaxAgeAndConditional304WithoutMakingOldDataLookFresh()
    {
        using var fixture = new Fixture { CacheSeconds = 120 };
        var original = await fixture.Source.GetRosterAsync(fixture.Token);
        var calls = fixture.Requests.Count;
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(60);
        var cached = await fixture.Source.GetRosterAsync(fixture.Token);
        Assert.Equal(calls, fixture.Requests.Count);
        Assert.Equal(original.ObservedAtUtc, cached.ObservedAtUtc);
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(61);
        fixture.NotModified = true;
        var revalidated = await fixture.Source.GetRosterAsync(fixture.Token);
        Assert.Equal(original.ObservedAtUtc, revalidated.ObservedAtUtc);
        Assert.Contains(
            fixture.Requests,
            request => request.ConditionalETag == "\"document-v1\"" && request.IfModifiedSince == original.ObservedAtUtc
        );
    }

    [Fact]
    public async Task NoStoreOnRevalidationEvictsTheCachedDocument()
    {
        using var fixture = new Fixture { CacheSeconds = 120 };
        await fixture.Source.GetRosterAsync(fixture.Token);
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(121);
        fixture.NotModified = true;
        fixture.NoStore = true;
        await fixture.Source.GetRosterAsync(fixture.Token);
        var calls = fixture.Requests.Count;
        await fixture.Source.GetRosterAsync(fixture.Token);
        Assert.True(fixture.Requests.Count > calls);
        var lastRoster = fixture.Requests.Last(request => request.Path.EndsWith("/roster", StringComparison.Ordinal));
        Assert.Null(lastRoster.ConditionalETag);
    }

    [Fact]
    public async Task UnauthorizedResourceRenewsTokenOnceAndTokenExpiryIsRespected()
    {
        using var fixture = new Fixture { UnauthorizedOnce = true };
        await fixture.Source.GetRosterAsync(fixture.Token);
        Assert.Equal(2, fixture.OAuthCalls);
        fixture.Clock.Now = fixture.Clock.Now.AddHours(1);
        await fixture.Source.GetRosterAsync(fixture.Token);
        Assert.Equal(3, fixture.OAuthCalls);
    }

    [Fact]
    public async Task RateLimitBacksOffAllRequestsAndDoesNotExposePrivateResponseData()
    {
        using var fixture = new Fixture { ResourceStatus = HttpStatusCode.TooManyRequests };
        var error = await Assert.ThrowsAsync<BlizzardApiException>(() => fixture.Source.GetRosterAsync(fixture.Token));
        Assert.Equal(HttpStatusCode.TooManyRequests, error.StatusCode);
        Assert.DoesNotContain("private-token", error.ToString());
        var calls = fixture.Requests.Count;
        await Assert.ThrowsAsync<BlizzardApiException>(() => fixture.Source.GetRosterAsync(fixture.Token));
        Assert.Equal(calls, fixture.Requests.Count);
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(121);
        fixture.ResourceStatus = HttpStatusCode.OK;
        Assert.Single((await fixture.Source.GetRosterAsync(fixture.Token)).Characters);
    }

    [Theory]
    [InlineData(301)]
    [InlineData(403)]
    [InlineData(503)]
    public async Task HttpFailuresAreNeverAcceptedAsAnEmptyRoster(int status)
    {
        using var fixture = new Fixture { ResourceStatus = (HttpStatusCode)status };
        var error = await Assert.ThrowsAsync<BlizzardApiException>(() => fixture.Source.GetRosterAsync(fixture.Token));
        Assert.Equal((HttpStatusCode)status, error.StatusCode);
        Assert.DoesNotContain("private", error.ToString());
    }

    [Fact]
    public async Task MissingGuildRosterIsExpectedUnavailabilityRatherThanAnEmptyRoster()
    {
        using var fixture = new Fixture { ResourceStatus = HttpStatusCode.NotFound };
        var error = await Assert.ThrowsAsync<RosterSourceUnavailableException>(() => fixture.Source.GetRosterAsync(fixture.Token));
        Assert.DoesNotContain("private", error.ToString());
    }

    [Theory]
    [InlineData("OAuth")]
    [InlineData("class-index")]
    public async Task Other404sAreNotMisclassifiedAsAnUnpublishedGuild(string endpoint)
    {
        using var fixture = new Fixture();
        if (endpoint == "OAuth")
            fixture.OAuthStatus = HttpStatusCode.NotFound;
        else
            fixture.ClassStatus = HttpStatusCode.NotFound;
        var error = await Assert.ThrowsAsync<BlizzardApiException>(() => fixture.Source.GetRosterAsync(fixture.Token));
        Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);
        Assert.Equal(endpoint == "OAuth" ? "OAuth" : "resource", error.Resource);
    }

    [Fact]
    public async Task InvalidAndOversizedJsonResponsesFailWithoutExposingBodyContent()
    {
        using var invalid = new Fixture { RosterJson = "private-token not JSON" };
        var error = await Assert.ThrowsAsync<BlizzardApiException>(() => invalid.Source.GetRosterAsync(invalid.Token));
        Assert.DoesNotContain("private-token", error.ToString());
        using var oversized = new Fixture { RosterJson = new string('x', 2 * 1024 * 1024 + 1) };
        var tooLarge = await Assert.ThrowsAsync<BlizzardApiException>(() => oversized.Source.GetRosterAsync(oversized.Token));
        Assert.Contains("size limit", tooLarge.Message);
        Assert.DoesNotContain(new string('x', 100), tooLarge.ToString());
    }

    [Fact]
    public void ConfigurationRequiresCredentialsAndRejectsUrlOrQueryTemplates()
    {
        var options = new BlizzardRosterOptions();
        Assert.Throws<ArgumentException>(options.Validate);
        options.ClientId = "public-client";
        options.ClientSecret = "private-secret";
        options.Validate();
        options.RosterPathTemplate = "https://example.test/data/wow/guild/{guild}/roster";
        Assert.Throws<ArgumentException>(options.Validate);
        options.RosterPathTemplate = "/data/wow/guild/{guild}/roster?access_token=private-secret";
        var error = Assert.Throws<ArgumentException>(options.Validate);
        Assert.DoesNotContain("private-secret", error.ToString());
    }

    private sealed class Fixture : IDisposable
    {
        public TestClock Clock { get; } = new();
        public string RosterJson { get; set; } = Roster;
        public string ProfessionJson { get; set; } = Professions;
        public HttpStatusCode ResourceStatus { get; set; } = HttpStatusCode.OK;
        public HttpStatusCode ProfessionStatus { get; set; } = HttpStatusCode.OK;
        public HttpStatusCode OAuthStatus { get; set; } = HttpStatusCode.OK;
        public HttpStatusCode ClassStatus { get; set; } = HttpStatusCode.OK;
        public int CacheSeconds { get; set; }
        public bool NotModified { get; set; }
        public bool NoStore { get; set; }
        public bool UnauthorizedOnce { get; set; }
        public int OAuthCalls { get; private set; }
        public List<Request> Requests { get; } = [];
        public BlizzardGuildRosterSource Source { get; }
        private readonly BlizzardApiClient _api;
        public CancellationToken Token => TestContext.Current.CancellationToken;

        public Fixture()
        {
            var options = new BlizzardRosterOptions { ClientId = "public-client", ClientSecret = "private-secret" };
            options.Validate();
            _api = new BlizzardApiClient(new HttpClient(new Handler(Send)), options, Clock);
            Source = new BlizzardGuildRosterSource(_api, options, Clock);
        }

        private HttpResponseMessage Send(HttpRequestMessage request)
        {
            Requests.Add(
                new(
                    request.RequestUri!.Host,
                    request.RequestUri.AbsolutePath,
                    request.RequestUri.Query,
                    request.Headers.Authorization?.Scheme,
                    request.Headers.IfNoneMatch.FirstOrDefault()?.ToString(),
                    request.Headers.IfModifiedSince?.UtcDateTime
                )
            );
            if (request.RequestUri.Host == "oauth.battle.net")
            {
                OAuthCalls++;
                Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
                Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("public-client:private-secret")), request.Headers.Authorization.Parameter);
                return Response(OAuthStatus, """{"access_token":"private-token","expires_in":3600}""");
            }
            Assert.Equal("private-token", request.Headers.Authorization!.Parameter);
            if (UnauthorizedOnce)
            {
                UnauthorizedOnce = false;
                return Response(HttpStatusCode.Unauthorized, "private-token");
            }
            if (ResourceStatus != HttpStatusCode.OK)
                return Response(ResourceStatus, "private-token private-secret");
            if (NotModified && request.Headers.IfNoneMatch.Count > 0)
                return Response(HttpStatusCode.NotModified, "");
            if (request.RequestUri.AbsolutePath.EndsWith("/professions", StringComparison.Ordinal))
                return Response(ProfessionStatus, ProfessionJson);
            return request.RequestUri.AbsolutePath.EndsWith("/roster", StringComparison.Ordinal)
                ? Response(HttpStatusCode.OK, RosterJson)
                : Response(ClassStatus, """{"classes":[{"id":1,"name":"Warrior"}]}""");
        }

        private HttpResponseMessage Response(HttpStatusCode status, string body)
        {
            var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            response.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromSeconds(CacheSeconds), NoStore = NoStore };
            response.Headers.ETag = new EntityTagHeaderValue("\"document-v1\"");
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(120));
            response.Content.Headers.LastModified = Clock.Now;
            return response;
        }

        public void Dispose() => _api.Dispose();
    }

    private sealed record Request(string Host, string Path, string Query, string? Scheme, string? ConditionalETag, DateTime? IfModifiedSince);

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 4, 21, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(send(request));
    }
}
