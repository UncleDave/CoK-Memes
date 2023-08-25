using System.Text;
using ChampionsOfKhazad.Bot.ChatBot;
using ChampionsOfKhazad.Bot.HardcoreStats.CharacterDeaths;
using MediatR;

namespace ChampionsOfKhazad.Bot;

public class RipSlashCommand : INotificationHandler<RipSlashCommandExecuted>
{
    private readonly Assistant _assistant;
    private readonly IPublisher _publisher;

    public RipSlashCommand(Assistant assistant, IPublisher publisher)
    {
        _assistant = assistant;
        _publisher = publisher;
    }

    public async Task Handle(RipSlashCommandExecuted notification, CancellationToken cancellationToken)
    {
        var character = notification.Command.Data.Options.Single(x => x.Name == "character").Value.ToString()!;
        var level = notification.Command.Data.Options.SingleOrDefault(x => x.Name == "level")?.Value;
        var race = notification.Command.Data.Options.SingleOrDefault(x => x.Name == "race")?.Value.ToString();
        var characterClass = notification.Command.Data.Options.SingleOrDefault(x => x.Name == "class")?.Value.ToString();
        var causeOfDeath = notification.Command.Data.Options.SingleOrDefault(x => x.Name == "cause")?.Value.ToString();
        var characterLevel = level is long levelLong ? (ushort?)Math.Clamp(levelLong, 1, 60) : null;

        var promptBuilder = new StringBuilder($"{character} has died.");
        var details = new List<string>();

        if (characterLevel is not null)
            details.Add($"level {characterLevel}");

        if (race is not null)
            details.Add(race);

        if (characterClass is not null)
            details.Add(characterClass);

        if (details.Count > 0)
            promptBuilder.Append(
                $" They were {(characterLevel is not null && details.Count == 1 ? string.Empty : "a ")}{string.Join(" ", details)}."
            );

        if (causeOfDeath is not null)
            promptBuilder.Append($" Their reported cause of death was {causeOfDeath}.");

        promptBuilder.Append(
            " Write an obituary for them, it must be less than 100 words. It must contain all the information you have been given about the character and their cause of death."
        );

        var obituaryTask = _assistant.RespondAsync(
            "You are the Dwarf Lorekeeper of a \"World of Warcraft Classic\" guild known as Champions of Khazad.",
            promptBuilder.ToString()
        );

        await notification.Command.DeferAsync();

        var obituary = await obituaryTask;

        await _publisher.Publish(
            new CharacterDeathReported(
                new CharacterDeath(
                    notification.Command.User.Id,
                    character,
                    notification.Command.CreatedAt,
                    obituary,
                    characterLevel,
                    race,
                    characterClass,
                    causeOfDeath
                )
            ),
            cancellationToken
        );

        await notification.Command.FollowupAsync(obituary);
    }
}
