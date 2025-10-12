using System.Security.Claims;
using Auth0.AspNetCore.Authentication;
using ChampionsOfKhazad.Bot.Core;
using ChampionsOfKhazad.Bot.GenAi;
using ChampionsOfKhazad.Bot.Lore;
using ChampionsOfKhazad.Bot.Lore.Abstractions;
using ChampionsOfKhazad.Bot.Portal;
using Discord;
using Discord.Rest;
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

builder.Services.AddSingleton(authOptions);

var guildId = builder.Configuration.GetValue<ulong>("GuildId");

// TODO: Access denied page for unauthorized users

var guildPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().AddRequirements(new DiscordGuildRequirement(guildId)).Build();
var adminPolicy = new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .RequireClaim(ClaimTypes.NameIdentifier, authOptions.AdminUserIds)
    .Build();

builder
    .Services.AddAuthorizationBuilder()
    .AddDefaultPolicy("default", guildPolicy)
    .AddFallbackPolicy("fallback", guildPolicy)
    .AddPolicy("admin", adminPolicy);

builder
    .Services.AddSingleton<IAuthorizationHandler, DiscordGuildAuthorizationHandler>()
    .AddSingleton<IAuthorizationHandler, AdminAuthorizationHandler>();

if (builder.Environment.IsDevelopment())
    builder.Services.AddSpaYarp();

builder
    .Services.AddBot(configuration =>
    {
        configuration.Persistence.ConnectionString = builder.Configuration.GetRequiredConnectionString("Mongo");
    })
    .AddEmbeddings(configuration =>
    {
        configuration.OpenAiApiKey = builder.Configuration.GetRequiredString("OpenAi:ApiKey");
    })
    .AddGuildLore()
    .AddMongoPersistence()
    .AddGenAiMongoPersistence();

builder
    .Services.AddSingleton<DiscordRestClient>()
    .AddSingleton(
        new DiscordClientProviderOptions(
            builder.Configuration.GetValue<string>("BotToken") ?? throw new MissingConfigurationValueException("BotToken")
        )
    )
    .AddSingleton<DiscordClientProvider>()
    .AddSingleton(new DiscordUserResolverOptions(guildId))
    .AddSingleton<DiscordUserResolver>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

var apiGroup = app.MapGroup("api");

apiGroup.MapGet(
    "me",
    (ClaimsPrincipal claimsPrincipal) =>
    {
        var userId = claimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId is null)
            return Results.Unauthorized();

        IEnumerable<string> roles = authOptions.UserIsAdmin(userId) ? ["Admin"] : [];

        return Results.Ok(new UserContract(roles));
    }
);

var loreGroup = apiGroup.MapGroup("lore").RequireAuthorization(adminPolicy);

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

var guildLore = apiGroup.MapGroup("guild-lore").RequireAuthorization(adminPolicy);

guildLore.MapPut(
    "{name}",
    async (string name, UpdateGuildLoreContract contract, IUpdateLore loreUpdater) =>
    {
        await loreUpdater.UpdateLoreAsync(new GuildLore(name, contract.Content));
        return Results.NoContent();
    }
);

var memberLore = apiGroup.MapGroup("member-lore").RequireAuthorization(adminPolicy);

memberLore.MapPut(
    "{name}",
    async (string name, UpdateMemberLoreContract contract, IUpdateLore loreUpdater) =>
    {
        await loreUpdater.UpdateLoreAsync(
            new MemberLore(name, contract.Pronouns, contract.Nationality, contract.MainCharacter, contract.Biography)
            {
                Aliases = contract.Aliases ?? [],
                Roles = contract.Roles ?? [],
            }
        );
        return Results.NoContent();
    }
);

var generatedImages = apiGroup.MapGroup("generated-images");

generatedImages.MapGet(
    "",
    async (
        IGeneratedImageStore generatedImageStore,
        DiscordUserResolver discordUserResolver,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken,
        string? query = null,
        ushort skip = 0,
        ushort take = 20,
        bool mine = false,
        bool sortAscending = false
    ) =>
    {
        var images = await generatedImageStore.GetAsync(
            skip,
            take,
            mine ? claimsPrincipal.GetDiscordUserId() : null,
            sortAscending,
            query,
            cancellationToken
        );

        var uniqueUserIds = images.Select(x => x.UserId).Distinct().ToList();
        var users = await Task.WhenAll(uniqueUserIds.Select(discordUserResolver.GetUserAsync));

        var contracts = images
            .Select(x =>
            {
                var user = users.Single(u => u.Id == x.UserId);
                var userName = user is IGuildUser guildUser ? guildUser.DisplayName : user.GlobalName ?? user.Username;
                var userContract = new GeneratedImageUserContract(userName, user.GetDisplayAvatarUrl());

                return new GeneratedImageContract(x.Prompt, userContract, x.Timestamp, x.Filename);
            })
            .ToList();

        return Results.Ok(contracts);
    }
);

if (builder.Environment.IsDevelopment())
    app.UseSpaYarp();
else
    app.MapStaticAssets();

app.MapFallbackToFile("index.html");

app.Run();
