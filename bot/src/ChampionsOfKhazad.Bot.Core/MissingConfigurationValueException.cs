namespace ChampionsOfKhazad.Bot.Core;

public class MissingConfigurationValueException : ApplicationException
{
    public MissingConfigurationValueException(string propertyName)
        : base($"{propertyName} is required but was missing") { }
}
