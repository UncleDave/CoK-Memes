using ChampionsOfKhazad.Bot.BlizzardProbe;
using Microsoft.Extensions.Configuration;

if (args is ["--help"])
{
    Console.WriteLine(
        "Forever API probe: --region eu|us|tw|kr --static-build 1.60.1_70399 [--character first-surname] [--guild slug [--realm slug]]"
    );
    Console.WriteLine("Credentials: dedicated user secrets Blizzard:ClientId / Blizzard:ClientSecret.");
    Console.WriteLine("Environment overrides: COK_PROBE_Blizzard__ClientId / COK_PROBE_Blizzard__ClientSecret.");
    return 0;
}

ProbeOptions options;
try
{
    options = ProbeOptions.Parse(args);
}
catch (ArgumentException e)
{
    // Only our option validation messages are safe to display, not arbitrary library exceptions.
    Console.Error.WriteLine(e.Message);
    return 1;
}

try
{
    var configuration = new ConfigurationBuilder().AddUserSecrets<ProbeOptions>().AddEnvironmentVariables("COK_PROBE_").Build();
    var clientId = configuration["Blizzard:ClientId"];
    var clientSecret = configuration["Blizzard:ClientSecret"];
    if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
    {
        Console.Error.WriteLine("Set Blizzard:ClientId and Blizzard:ClientSecret in user secrets with ID cok-forever-api-probe; see bot/README.md.");
        return 1;
    }

    using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cancellation.Cancel();
    };
    using var handler = new HttpClientHandler { AllowAutoRedirect = false };
    using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    return await new BlizzardApiProbe(http, Console.Out).RunAsync(options, clientId, clientSecret, cancellation.Token);
}
catch (Exception)
{
    // Transport/configuration exceptions can contain request details. Never dump them or response bodies.
    Console.Error.WriteLine("Probe failed or was cancelled; details suppressed to protect credentials. Check local configuration and connectivity.");
    return 1;
}
