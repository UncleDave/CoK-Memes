using System.Security.Claims;
using Auth0.AspNetCore.Authentication;
using ChampionsOfKhazad.Bot.Core;
using ChampionsOfKhazad.Bot.Lore;
using ChampionsOfKhazad.Bot.Portal;
using JetBrains.Annotations;
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

var apiGroup = app.MapGroup("api");
var loreGroup = apiGroup.MapGroup("lore");

loreGroup.MapGet(
    "",
    async (IGetLore loreGetter, CancellationToken cancellationToken) =>
        Results.Ok((await loreGetter.GetLoreAsync(cancellationToken)).OrderByDescending(x => x is GuildLore).ThenBy(x => x.Name))
);

loreGroup.MapGet(
    "{name}",
    async (string name, IGetLore loreGetter, CancellationToken cancellationToken) =>
    {
        var lore = await loreGetter.GetLoreAsync(name, cancellationToken);
        return lore is null ? Results.NotFound() : Results.Ok(lore);
    }
);

var guildLore = apiGroup.MapGroup("guild-lore");

guildLore.MapPut(
    "{name}",
    async (string name, UpdateGuildLoreContract contract, IUpdateLore loreUpdater) =>
    {
        await loreUpdater.UpdateLoreAsync(new GuildLore(name, contract.Content));
        return Results.NoContent();
    }
);

var memberLore = apiGroup.MapGroup("member-lore");

memberLore.MapPut(
    "{name}",
    async (string name, UpdateMemberLoreContract contract, IUpdateLore loreUpdater) =>
    {
        await loreUpdater.UpdateLoreAsync(
            new MemberLore(name, contract.Pronouns, contract.Nationality, contract.MainCharacter, contract.Biography)
            {
                Aliases = contract.Aliases ?? Array.Empty<string>(),
                Roles = contract.Roles ?? Array.Empty<string>(),
            }
        );
        return Results.NoContent();
    }
);

if (builder.Environment.IsDevelopment())
    app.UseSpaYarp();
else
    app.MapStaticAssets();

app.MapFallbackToFile("index.html");

app.Run();

namespace ChampionsOfKhazad.Bot.Portal
{
    [method: UsedImplicitly]
    public record UpdateGuildLoreContract(string Content);

    [method: UsedImplicitly]
    public record UpdateMemberLoreContract(
        string Pronouns,
        string Nationality,
        string MainCharacter,
        string? Biography,
        IReadOnlyList<string>? Aliases,
        IReadOnlyList<string>? Roles
    );
}
