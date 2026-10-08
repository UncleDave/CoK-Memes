using System.ClientModel;
using System.Text.Json;

namespace ChampionsOfKhazad.Bot.Tests;

public class GazetteImageFailureTests
{
    [Theory]
    [InlineData(400, "unsupported_parameter", "output_format")]
    [InlineData(403, "model_access_denied", "model")]
    [InlineData(429, "insufficient_quota", "prompt")]
    [InlineData(500, "server_error", "quality")]
    public void KnownProviderErrorsReportOnlySafeStatusCodeAndParameter(int status, string code, string parameter)
    {
        var failure = GazetteImageFailure.FromPayload(
            status,
            JsonSerializer.Serialize(
                new
                {
                    error = new
                    {
                        code,
                        param = parameter,
                        message = "SECRET generated prompt, user data and credentials",
                    },
                }
            )
        );
        Assert.Equal(status, failure.HttpStatus);
        Assert.Equal(code, failure.Code);
        Assert.Equal(parameter, failure.Parameter);
        Assert.Contains($"HTTP {status}", failure.Description);
        Assert.DoesNotContain("SECRET", failure.ToString());
    }

    [Theory]
    [InlineData("not JSON")]
    [InlineData("null")]
    [InlineData("""{"error":{"code":"SECRET","type":"SECRET","param":"SECRET","message":"SECRET"}}""")]
    public void UnknownOrMalformedFieldsCannotLeakProviderContent(string payload)
    {
        var failure = GazetteImageFailure.FromPayload(400, payload);
        Assert.Equal("unspecified", failure.Code);
        Assert.Equal("none", failure.Parameter);
        Assert.DoesNotContain("SECRET", failure.ToString());
    }

    [Fact]
    public void MissingCodeCanUseAKnownErrorTypeWithoutCopyingTheProviderMessage()
    {
        var failure = GazetteImageFailure.FromPayload(
            400,
            """{"error":{"code":null,"type":"invalid_request_error","param":"size","message":"SECRET"}}"""
        );
        Assert.Equal("invalid_request_error", failure.Code);
        Assert.Equal("size", failure.Parameter);
    }

    [Fact]
    public void LocalTimeoutAndApiClientFailureAreDistinctWithoutExceptionMessages()
    {
        Assert.True(GazetteImageFailure.FromException(new OperationCanceledException("SECRET")).TimedOut);
        var failure = GazetteImageFailure.FromException(new ClientResultException("SECRET"));
        Assert.Equal(0, failure.HttpStatus);
        Assert.False(failure.TimedOut);
        Assert.DoesNotContain("SECRET", failure.Description);
    }
}
