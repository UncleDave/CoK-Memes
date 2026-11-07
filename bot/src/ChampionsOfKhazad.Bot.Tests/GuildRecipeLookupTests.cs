using ChampionsOfKhazad.Bot.Roster;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using static ChampionsOfKhazad.Bot.Tests.RosterFixture;

namespace ChampionsOfKhazad.Bot.Tests;

public class GuildRecipeLookupTests
{
    private static KnownRecipe Recipe(string id = "1902", string name = "Hot Lion Chops") => new() { Id = id, Name = name };

    private static ProfessionObservation Recipes(params KnownRecipe[] recipes) =>
        Professions(null, id: "185", name: "Cooking") with
        {
            Skills =
            [
                new()
                {
                    Id = "185",
                    Name = "Cooking",
                    KnownRecipes = recipes,
                },
            ],
        };

    [Theory]
    [InlineData("Hot Lion Chops")]
    [InlineData("hot lion chops")]
    [InlineData("lion chop")]
    [InlineData("1902")]
    public async Task LooksUpExplicitRecipeKnowledgeIncludingSecondaryProfessionsAndUnknownRanks(string query)
    {
        using var fixture = new RosterFixture();
        fixture.SetMembers(Character(professions: Recipes(Recipe())), Character("2", professions: Professions(300)));
        await fixture.Refresh();
        var page = await fixture.Queries.FindRecipeCraftersAsync(50, query, cancellationToken: fixture.Token);
        Assert.Equal("ok", page.Status);
        Assert.True(page.IsMock);
        Assert.Equal("1902", page.Recipe!.Id);
        var crafter = Assert.Single(page.Crafters);
        Assert.Equal("1", crafter.Id);
        Assert.Equal("Borin Ironhide", crafter.FullName);
        Assert.Equal("Cooking", crafter.Profession);
        Assert.Null(crafter.Skill);
        Assert.NotNull(crafter.CheckedAtUtc);
        Assert.Equal(0, fixture.Source.ProfessionReads);
        Assert.Empty(fixture.Notices.Items);
    }

    [Fact]
    public async Task ResolvingARecipeNameIncludesEveryCrafterWithTheSameIdEvenIfItsRecordedNameChanged()
    {
        using var fixture = new RosterFixture();
        fixture.SetMembers(Character(professions: Recipes(Recipe())), Character("2", professions: Recipes(Recipe(name: "Previous Name"))));
        await fixture.Refresh();
        var page = await fixture.Queries.FindRecipeCraftersAsync(50, "Hot Lion Chops", cancellationToken: fixture.Token);
        Assert.Equal("ok", page.Status);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(["1", "2"], page.Crafters.Select(crafter => crafter.Id).ToArray());
    }

    [Fact]
    public async Task ExactNameWinsAndPartialOrDuplicateNamesReturnPaginatedChoicesInsteadOfGuessing()
    {
        using var fixture = new RosterFixture();
        var recipes = Enumerable.Range(1, 12).Select(index => Recipe(index.ToString(), $"Potion {index:D2}")).ToArray();
        fixture.SetMembers(Character(professions: Recipes(recipes)));
        await fixture.Refresh();
        var exact = await fixture.Queries.FindRecipeCraftersAsync(50, "Potion 01", cancellationToken: fixture.Token);
        Assert.Equal("ok", exact.Status);
        Assert.Equal("1", exact.Recipe!.Id);
        var first = await fixture.Queries.FindRecipeCraftersAsync(50, "Potion", cancellationToken: fixture.Token);
        var second = await fixture.Queries.FindRecipeCraftersAsync(50, "Potion", page: 2, cancellationToken: fixture.Token);
        Assert.Equal("ambiguous_recipe", first.Status);
        Assert.Equal(10, first.RecipeChoices.Length);
        Assert.Equal(2, first.NextPage);
        Assert.Equal(2, second.RecipeChoices.Length);
        Assert.Null(second.NextPage);
        Assert.Empty(first.Crafters);
        Assert.Equal(12, second.TotalCount);
        Assert.Equal("page_out_of_range", (await fixture.Queries.FindRecipeCraftersAsync(50, "Potion", int.MaxValue, fixture.Token)).Status);

        fixture.Advance();
        fixture.SetMembers(Character(professions: Recipes(Recipe("1", "Herbalism"), Recipe("2", "Herbalism"))));
        await fixture.Refresh();
        var duplicateName = await fixture.Queries.FindRecipeCraftersAsync(50, "Herbalism", cancellationToken: fixture.Token);
        Assert.Equal("ambiguous_recipe", duplicateName.Status);
        Assert.Equal(2, duplicateName.RecipeChoices.Length);
        Assert.Equal("ok", (await fixture.Queries.FindRecipeCraftersAsync(50, "2", cancellationToken: fixture.Token)).Status);
    }

    [Fact]
    public async Task CraftersAreDeduplicatedAndSortedByNameAcrossBoundedPagesWithWholeGuildCoverage()
    {
        using var fixture = new RosterFixture();
        var observations = Recipes(Recipe());
        var skill = observations.Skills[0];
        observations = observations with { Skills = [skill, skill with { Tier = "other" }] };
        fixture.SetMembers(
            Enumerable
                .Range(1, 12)
                .Select(index => Character(index.ToString(), first: $"Player{index:D2}", professions: observations))
                .Append(Character("missing"))
                .ToArray()
        );
        await fixture.Refresh();
        var first = await fixture.Queries.FindRecipeCraftersAsync(50, "1902", cancellationToken: fixture.Token);
        var second = await fixture.Queries.FindRecipeCraftersAsync(50, "1902", page: 2, cancellationToken: fixture.Token);
        Assert.Equal(12, first.TotalCount);
        Assert.Equal(10, first.Crafters.Length);
        Assert.Equal(2, second.Crafters.Length);
        Assert.Equal("Player11 Ironhide", second.Crafters[0].FullName);
        Assert.Equal(13, second.Coverage!.TotalCharacters);
        Assert.Equal(1, second.Coverage.AffectedCharacters);
        Assert.Equal(first.Coverage, second.Coverage);
        Assert.Equal("page_out_of_range", (await fixture.Queries.FindRecipeCraftersAsync(50, "1902", int.MaxValue, fixture.Token)).Status);
    }

    [Fact]
    public async Task MissingEmptyAndPartialRecipeDataHaveDistinctCoverageAndRanksNeverImplyKnowledge()
    {
        using var fixture = new RosterFixture();
        fixture.SetMembers(
            Character(),
            Character("2", professions: Professions(300)),
            Character("3", professions: Recipes()),
            Character("4", professions: new() { Complete = true }),
            Character("5", professions: Recipes() with { Complete = false })
        );
        await fixture.Refresh();
        var page = await fixture.Queries.FindRecipeCraftersAsync(50, "Hot Lion Chops", cancellationToken: fixture.Token);
        Assert.Equal("no_match", page.Status);
        Assert.Empty(page.Crafters);
        Assert.Equal(5, page.Coverage!.TotalCharacters);
        Assert.Equal(1, page.Coverage.UnavailableCharacters);
        Assert.Equal(2, page.Coverage.PartialCharacters);
        Assert.Equal(3, page.Coverage.AffectedCharacters);
        Assert.Equal(0, page.Coverage.FailedRefreshCharacters);
    }

    [Fact]
    public async Task RecipeUpdatesRetainPriorDataOnFailureAndStaleResponsesButEmptyListsAndDeparturesRemoveMatches()
    {
        using var fixture = new RosterFixture();
        fixture.Source.HasSeparateProfessionRequests = true;
        fixture.Source.Professions["id:1"] = Recipes(Recipe()) with { ObservedAtUtc = fixture.Clock.Now.UtcDateTime };
        await fixture.Refresh();
        var original = Assert.Single((await fixture.Queries.FindRecipeCraftersAsync(50, "1902", cancellationToken: fixture.Token)).Crafters);

        fixture.Advance(600);
        fixture.Source.ProfessionFailure = new IOException("Private source detail");
        await fixture.Refresh();
        var failed = await fixture.Queries.FindRecipeCraftersAsync(50, "1902", cancellationToken: fixture.Token);
        var retained = Assert.Single(failed.Crafters);
        Assert.True(retained.RefreshFailed);
        Assert.Equal(original.ObservedAtUtc, retained.ObservedAtUtc);
        Assert.True(retained.CheckedAtUtc > original.CheckedAtUtc);
        Assert.Equal(1, failed.Coverage!.FailedRefreshCharacters);

        fixture.Advance(600);
        fixture.Source.ProfessionFailure = null;
        fixture.Source.Professions["id:1"] = Recipes() with { ObservedAtUtc = original.ObservedAtUtc.AddDays(-1) };
        await fixture.Refresh();
        Assert.Single((await fixture.Queries.FindRecipeCraftersAsync(50, "1902", cancellationToken: fixture.Token)).Crafters);

        fixture.Advance(600);
        fixture.Source.Professions["id:1"] = Recipes();
        await fixture.Refresh();
        var empty = await fixture.Queries.FindRecipeCraftersAsync(50, "1902", cancellationToken: fixture.Token);
        Assert.Equal("no_match", empty.Status);
        Assert.Equal(0, empty.Coverage!.AffectedCharacters);

        fixture.Advance(600);
        fixture.Source.Professions["id:1"] = Recipes(Recipe());
        await fixture.Refresh();
        Assert.Single((await fixture.Queries.FindRecipeCraftersAsync(50, "1902", cancellationToken: fixture.Token)).Crafters);
        fixture.Advance();
        fixture.SetMembers();
        await fixture.Refresh();
        Assert.Equal("empty", (await fixture.Queries.FindRecipeCraftersAsync(50, "1902", cancellationToken: fixture.Token)).Status);
    }

    [Fact]
    public async Task UnavailableInvalidNoDataAndFailedStatesStayDistinctAndCannotCrossGuildScopes()
    {
        using var fixture = new RosterFixture();
        Assert.Equal("unavailable", (await fixture.Queries.FindRecipeCraftersAsync(51, "1902", cancellationToken: fixture.Token)).Status);
        fixture.Options.Enabled = false;
        Assert.Equal("unavailable", (await fixture.Queries.FindRecipeCraftersAsync(50, "1902", cancellationToken: fixture.Token)).Status);
        fixture.Options.Enabled = true;
        foreach (var query in new[] { "", " ", new string('x', 201), "Potion\nName" })
            Assert.Equal("invalid_query", (await fixture.Queries.FindRecipeCraftersAsync(50, query, cancellationToken: fixture.Token)).Status);
        Assert.Equal("invalid_query", (await fixture.Queries.FindRecipeCraftersAsync(50, "1902", 0, fixture.Token)).Status);
        Assert.Equal(0, fixture.Store.Reads);
        Assert.Equal("no_data", (await fixture.Queries.FindRecipeCraftersAsync(50, "1902", cancellationToken: fixture.Token)).Status);
        fixture.Source.Failure = new IOException();
        await Assert.ThrowsAsync<IOException>(fixture.Refresh);
        Assert.Equal("refresh_failed", (await fixture.Queries.FindRecipeCraftersAsync(50, "1902", cancellationToken: fixture.Token)).Status);
        fixture.Store.FailReads = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Queries.FindRecipeCraftersAsync(50, "1902", cancellationToken: fixture.Token)
        );
    }

    [Fact]
    public void RecipeValidationBoundsNamesCountsAndDuplicateIdsButAllowsDifferentIdsWithSameName()
    {
        var now = DateTime.UtcNow;
        foreach (
            var invalid in new[]
            {
                new[] { Recipe(), Recipe() },
                new[] { Recipe(name: " ") },
                new[] { Recipe(name: "Control\nName") },
                new[] { Recipe(name: new string('x', 201)) },
                new[] { Recipe(id: "") },
                new KnownRecipe[] { null! },
                Enumerable.Range(0, 5001).Select(index => Recipe(index.ToString())).ToArray(),
            }
        )
            Assert.Throws<InvalidDataException>(() => RosterValidator.Validate(Recipes(invalid), now, now));
        Assert.Equal(2, RosterValidator.Validate(Recipes(Recipe("1"), Recipe("2")), now, now).Skills[0].KnownRecipes!.Length);
    }

    [Fact]
    public void BsonRoundtripPreservesRecipeKnowledgeAndOldDocumentsRemainUnknown()
    {
        var state = new RosterState
        {
            Id = "50:mock",
            Characters =
            [
                Character(professions: Recipes(Recipe())),
                Character("2", professions: Recipes()),
                Character("3", professions: Professions()),
            ],
        };
        var restored = BsonSerializer.Deserialize<RosterState>(state.ToBson());
        Assert.Equal(Recipe(), Assert.Single(restored.Characters[0].Professions!.Skills[0].KnownRecipes!));
        Assert.Empty(restored.Characters[1].Professions!.Skills[0].KnownRecipes!);
        Assert.Null(restored.Characters[2].Professions!.Skills[0].KnownRecipes);

        var legacy = state.ToBsonDocument();
        foreach (var member in legacy[Field<RosterState>(nameof(RosterState.Characters))].AsBsonArray)
            member[Field<RosterCharacter>(nameof(RosterCharacter.Professions))]
                [Field<ProfessionObservation>(nameof(ProfessionObservation.Skills))][0]
                .AsBsonDocument.Remove(Field<ProfessionSkill>(nameof(ProfessionSkill.KnownRecipes)));
        var old = BsonSerializer.Deserialize<RosterState>(legacy);
        Assert.All(old.Characters, character => Assert.Null(character.Professions!.Skills[0].KnownRecipes));
    }

    private static string Field<T>(string property) => BsonClassMap.LookupClassMap(typeof(T)).GetMemberMap(property).ElementName;
}
