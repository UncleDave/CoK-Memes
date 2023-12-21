using System.Security.Claims;
using Auth0.AspNetCore.Authentication;
using ChampionsOfKhazad.Bot.Core;
using ChampionsOfKhazad.Bot.Lore;
using ChampionsOfKhazad.Bot.Portal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);
var authOptions = builder.Configuration.GetSection("Auth").Get<AuthOptions>() ?? throw new MissingConfigurationValueException("Auth");

builder
    .Services.AddAuthentication(options =>
    {
        var auth0WebAppOptions = new Auth0WebAppOptions();

        options.DefaultScheme = auth0WebAppOptions.CookieAuthenticationScheme;
        options.DefaultChallengeScheme = options.DefaultSignOutScheme = Auth0Constants.AuthenticationScheme;
    })
    .AddAuth0WebAppAuthentication(options =>
    {
        options.Domain = authOptions.Domain;
        options.ClientId = authOptions.ClientId;
        options.ClientSecret = authOptions.ClientSecret;
        options.ResponseType = OpenIdConnectResponseType.Code;
    });

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim(ClaimTypes.NameIdentifier, authOptions.AllowedUserIds)
        .Build();
});

if (builder.Environment.IsDevelopment())
    builder.Services.AddSpaYarp();

builder
    .Services.AddBot(configuration =>
    {
        configuration.Persistence.ConnectionString = builder.Configuration.GetRequiredConnectionString("Mongo");
    })
    .AddGuildLore()
    .AddMongoPersistence();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("api");
var lore = api.MapGroup("lore");

lore.MapGet("", async (IGetLore loreGetter, CancellationToken cancellationToken) => Results.Ok(await loreGetter.GetLoreAsync(cancellationToken)));

lore.MapGet(
    "{name}",
    async (string name, IGetLore loreGetter, CancellationToken cancellationToken) =>
        Results.Ok(await loreGetter.GetLoreAsync(name, cancellationToken))
);

var guildLore = api.MapGroup("guild-lore");

guildLore.MapPut(
    "{name}",
    async (string name, UpdateGuildLoreContract contract, IUpdateLore loreUpdater) =>
    {
        await loreUpdater.UpdateLoreAsync(new GuildLore(name, contract.Content));
        return Results.NoContent();
    }
);

app.UseStaticFiles();

if (builder.Environment.IsDevelopment())
    app.UseSpaYarp();

app.MapFallbackToFile("index.html");

app.Run();

public record UpdateGuildLoreContract(string Content);
