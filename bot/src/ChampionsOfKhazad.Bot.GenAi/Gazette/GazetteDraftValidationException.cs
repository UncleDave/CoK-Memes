namespace ChampionsOfKhazad.Bot.GenAi;

// Only fixed categories/field names and counts are exposed; never retain rejected prose or source data.
public sealed class GazetteDraftValidationException(
    GazetteValidationFailure failure,
    GazetteValidationField field,
    int? actualLength = null,
    int? limit = null
) : InvalidOperationException($"Gazette draft validation failed: {failure}/{field}.")
{
    public GazetteValidationFailure Failure { get; } = failure;
    public GazetteValidationField Field { get; } = field;
    public int? ActualLength { get; } = actualLength;
    public int? Limit { get; } = limit;
}
