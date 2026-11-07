namespace ChampionsOfKhazad.Bot.Roster;

public record RecipePage
{
    public string Status { get; init; } = "ok";
    public bool IsMock { get; init; }
    public string GuildName { get; init; } = "";
    public bool LastRefreshFailed { get; init; }
    public RecipeCoverage? Coverage { get; init; }
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int TotalCount { get; init; }
    public int? NextPage => Page < TotalPages ? Page + 1 : null;
    public KnownRecipe? Recipe { get; init; }
    public KnownRecipe[] RecipeChoices { get; init; } = [];
    public RecipeCrafter[] Crafters { get; init; } = [];
}
