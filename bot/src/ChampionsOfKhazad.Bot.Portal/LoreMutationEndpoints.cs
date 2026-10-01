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
            async (string name, UpdateMemberLoreContract contract, IUpdateLore loreUpdater, CancellationToken cancellationToken) =>
            {
                var updated = await loreUpdater.UpdateLoreAsync(
                    new MemberLore(name, contract.Pronouns, contract.Nationality, contract.MainCharacter, contract.Biography)
                    {
                        Aliases = contract.Aliases ?? [],
                        Roles = contract.Roles ?? [],
                    },
                    cancellationToken
                );
                return updated ? Results.NoContent() : Results.NotFound();
            }
        );
        memberLore.MapPost(
            "",
            async (CreateMemberLoreContract contract, ICreateLore loreCreator, CancellationToken cancellationToken) =>
            {
                var created = await loreCreator.CreateLoreAsync(
                    new MemberLore(contract.Name, contract.Pronouns, contract.Nationality, contract.MainCharacter, contract.Biography)
                    {
                        Aliases = contract.Aliases ?? [],
                        Roles = contract.Roles ?? [],
                    },
                    cancellationToken
                );
                if (!created)
                    return Results.Conflict(new { message = "Lore with this name already exists." });
                return Results.Created($"/api/lore/{Uri.EscapeDataString(contract.Name)}", null);
            }
        );
        return apiGroup;
    }
}
