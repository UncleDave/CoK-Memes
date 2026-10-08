namespace ChampionsOfKhazad.Bot.GenAi;

public enum GazetteValidationFailure
{
    InputTooLarge,
    ResponseTooLarge,
    IncompleteResponse,
    InvalidJson,
    InvalidShape,
    InvalidFieldType,
    FieldTooLong,
    MissingText,
    InvalidArticleCount,
    MultilineText,
    InvalidCitationCount,
    InvalidCitation,
    DuplicateCitation,
    MissingClassified,
    UnexpectedFiller,
}
