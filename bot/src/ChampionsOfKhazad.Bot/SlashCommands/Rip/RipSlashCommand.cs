using ChampionsOfKhazad.Bot.DiscordMemes.CharacterDeaths;
using ChampionsOfKhazad.Bot.GenAi;
using MediatR;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ChampionsOfKhazad.Bot;

public class RipSlashCommand(IPublisher publisher, ICompletionService completionService) : INotificationHandler<RipSlashCommandExecuted>
{
    public async Task Handle(RipSlashCommandExecuted notification, CancellationToken cancellationToken)
    {
        var character = (string)notification.Command.Data.Options.Single(x => x.Name == "character").Value;
        var level = (ushort)Math.Clamp((long)notification.Command.Data.Options.Single(x => x.Name == "level").Value, 1, 60);
        var race = (string)notification.Command.Data.Options.Single(x => x.Name == "race").Value;
        var characterClass = (string)notification.Command.Data.Options.Single(x => x.Name == "class").Value;
        var causeOfDeath = (string)notification.Command.Data.Options.Single(x => x.Name == "cause").Value;

        var obituaryTask = completionService.InvokeAsync(
            new ChatHistory(
                string.Join(
                    '\n',
                    "You are the Dwarf Lorekeeper of a World of Warcraft Classic guild known as Champions of Khazad.",
                    $"{character}, a level {level} {race} {characterClass}, has died. Their reported cause of death was {causeOfDeath}. Write an obituary for them, it must be less than 100 words. It must contain all the information you have been given about the character and their cause of death."
                )
            ),
            cancellationToken
        );

        await notification.Command.DeferAsync();

        var obituary = await obituaryTask;

        await publisher.Publish(
            new CharacterDeathReported(
                new CharacterDeath(
                    notification.Command.User.Id,
                    character,
                    notification.Command.CreatedAt,
                    obituary,
                    level,
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
