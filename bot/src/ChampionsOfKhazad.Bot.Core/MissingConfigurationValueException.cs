namespace ChampionsOfKhazad.Bot.Core;

public class MissingConfigurationValueException(string propertyName) : ApplicationException($"{propertyName} is required but was missing");
