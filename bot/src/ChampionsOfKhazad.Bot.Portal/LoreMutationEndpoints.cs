using System.Globalization;
using ChampionsOfKhazad.Bot.Lore;
using ChampionsOfKhazad.Bot.Lore.Abstractions;
using Microsoft.AspNetCore.Authorization;

namespace ChampionsOfKhazad.Bot.Portal;

public static class LoreMutationEndpoints
{
    public static RouteGroupBuilder MapLoreMutations(this RouteGroupBuilder apiGroup, AuthorizationPolicy adminPolicy)
    {
        var guildLore = apiGroup.MapGroup("guild-lore").RequireAuthorization(adminPolicy);
        guildLore.MapPut(
            "{name}",
            async (string name, UpdateGuildLoreContract contract, IUpdateLore loreUpdater, CancellationToken cancellationToken) =>
            {
                var updated = await loreUpdater.UpdateLoreAsync(new GuildLore(name, contract.Content), cancellationToken);
                return updated ? Results.NoContent() : Results.NotFound();
            }
        );
        guildLore.MapPost(
            "",
            async (CreateGuildLoreContract contract, ICreateLore loreCreator, CancellationToken cancellationToken) =>
            {
                if (!await loreCreator.CreateLoreAsync(new GuildLore(contract.Name, contract.Content), cancellationToken))
                    return Results.Conflict(new { message = "Lore with this name already exists." });
                return Results.Created($"/api/lore/{Uri.EscapeDataString(contract.Name)}", null);
            }
        );

        var memberLore = apiGroup.MapGroup("member-lore").RequireAuthorization(adminPolicy);
        memberLore.MapPut(
            "{name}",
            async (
                string name,
                UpdateMemberLoreContract contract,
                IEditLoreStore loreEditor,
                IGetLore loreGetter,
                CancellationToken cancellationToken
            ) =>
            {
                if (!TryParseDiscordUserId(contract.DiscordUserId, out var discordUserId))
                    return InvalidDiscordUserId();

                var expected = await loreEditor.GetEntryAsync(name, cancellationToken);
                if (!expected.Exists || expected.Entry is not { Kind: "member" } existing)
                    return Results.NotFound();
                if (contract.DiscordUserId is null)
                    discordUserId = existing.DiscordUserId;

                if (await FindDiscordUserIdConflictAsync(discordUserId, loreGetter, expected.Name, cancellationToken) is { } conflict)
                    return Results.Conflict(new { message = conflict });

                var after = new LoreEntrySnapshot(expected.Name, "member")
                {
                    Pronouns = contract.Pronouns,
                    Nationality = contract.Nationality,
                    MainCharacter = contract.MainCharacter,
                    Biography = contract.Biography,
                    Aliases = contract.Aliases ?? [],
                    Roles = contract.Roles ?? [],
                    DiscordUserId = discordUserId,
                };
                var revision = new LoreRevision(Guid.NewGuid().ToString("N"), DateTime.UtcNow, "lore-store", null, "save", existing, after);
                if (await loreEditor.TrySaveAsync(expected, revision, cancellationToken))
                    return Results.NoContent();
                if (await FindDiscordUserIdConflictAsync(discordUserId, loreGetter, expected.Name, cancellationToken) is { } writeConflict)
                    return Results.Conflict(new { message = writeConflict });
                return Results.Conflict(new { message = "This lore changed before your update could be saved. Reload the entry and try again." });
            }
        );
        memberLore.MapPost(
            "",
            async (CreateMemberLoreContract contract, ICreateLore loreCreator, IGetLore loreGetter, CancellationToken cancellationToken) =>
            {
                if (!TryParseDiscordUserId(contract.DiscordUserId, out var discordUserId))
                    return InvalidDiscordUserId();

                if (await FindDiscordUserIdConflictAsync(discordUserId, loreGetter, null, cancellationToken) is { } conflict)
                    return Results.Conflict(new { message = conflict });

                var created = await loreCreator.CreateLoreAsync(
                    new MemberLore(contract.Name, contract.Pronouns, contract.Nationality, contract.MainCharacter, contract.Biography)
                    {
                        Aliases = contract.Aliases ?? [],
                        Roles = contract.Roles ?? [],
                        DiscordUserId = discordUserId,
                    },
                    cancellationToken
                );
                if (!created)
                {
                    if (await FindDiscordUserIdConflictAsync(discordUserId, loreGetter, null, cancellationToken) is { } writeConflict)
                        return Results.Conflict(new { message = writeConflict });
                    return Results.Conflict(new { message = "Lore with this name already exists." });
                }
                return Results.Created($"/api/lore/{Uri.EscapeDataString(contract.Name)}", null);
            }
        );
        return apiGroup;
    }

    private static bool TryParseDiscordUserId(string? value, out ulong? discordUserId)
    {
        discordUserId = null;
        if (value is null or "")
            return true;
        if (
            value.Any(character => character is < '0' or > '9')
            || !ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            || parsed == 0
            || parsed > long.MaxValue
        )
            return false;
        discordUserId = parsed;
        return true;
    }

    private static IResult InvalidDiscordUserId() =>
        Results.BadRequest(
            new
            {
                message = "Discord user ID must contain only decimal digits, be greater than 0, and be at most 9223372036854775807. Leave it blank to unlink this member.",
            }
        );

    private static async Task<string?> FindDiscordUserIdConflictAsync(
        ulong? discordUserId,
        IGetLore loreGetter,
        string? currentName,
        CancellationToken cancellationToken
    )
    {
        if (discordUserId is null)
            return null;
        var linkedMember = await loreGetter.GetMemberLoreAsync(discordUserId.Value, cancellationToken);
        return linkedMember is null || string.Equals(linkedMember.Name, currentName, StringComparison.OrdinalIgnoreCase)
            ? null
            : $"Discord user ID {discordUserId.Value.ToString(CultureInfo.InvariantCulture)} is already linked to {linkedMember.Name}. Clear that member's Discord user ID before linking it here.";
    }
}
