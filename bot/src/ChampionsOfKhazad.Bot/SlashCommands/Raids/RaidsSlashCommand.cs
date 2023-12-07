using ChampionsOfKhazad.Bot.RaidHelper;
using Discord;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class RaidsSlashCommand(
    IOptions<RaidsSlashCommandOptions> options,
    RaidHelperClient raidHelperClient,
    BotContext botContext,
    ILogger<RaidsSlashCommand> logger
) : INotificationHandler<RaidsSlashCommandExecuted>
{
    private readonly RaidsSlashCommandOptions _options = options.Value;

    private static readonly string[] Acknowledgements = { "More work?", "Right-o.", "Yes, milord.", "All right.", "Off I go, then!" };

    private static readonly string[] RaidNames =
    {
        "Prepare To Wipe Edition",
        "The Movie: The Game",
        "The Movie: The Game: The Sequel",
        "Leaf's Sandwich Edition",
        "Green Eggs And Spinach Edition",
        "Naked Snakes Edition",
        "7°C",
        "0 Wipes, 0 Liver",
        "Eeler Fault",
        "Keep Absti Up",
        "Money Shoe",
        "We Beavin'",
        "Punctual Cokebeard Edition",
        "Woop Woop!",
        "Cycloning!",
        "Rigd Edition",
        "Big Mac Edition",
        "Whopper Edition",
        "Chesand Edition",
        "Beans On Toast Edition",
        "The Guild Disbands Edition",
        "The Search For Ponty",
        "Texas Edition",
        "Outshao Edition",
        "7 Drakes",
        "Mumquest",
        "Wooper vs Predator",
        "Frozen Coffee Edition",
        "Live From The Bath!",
        "Yoric Goes OOM Edition",
        "Free Gems Edition",
        "Rigd Edition",
        "Wooper's Cat Edition",
        "Rat Edition",
        "Camaraderie Edition",
    };

    private static readonly string[] RaidDescriptions =
    {
        "Bring your own glizzy.",
        "Yeet.",
        "If you haven't started pinging Cokebeard yet it's already too late.",
        "The one where Wooper finally gets pushed too far.",
        "Ask Beaver about his guns.",
        "Don't mass report Yoric again, it's not funny guys, come on.",
        "Jk, it's actually Naxx.",
        "The one where Sam and Mards finally kiss.",
        "Hey can we do some achievements?",
        "Stick around for the post-raid zeppelin show!",
        "Come, pig.",
        "I just can't wait for Ruby Sanctum!",
        "BOOOOOOOOOOOOOONESTOOOOOOOOOOOOORM",
        "🎵 _If you want any loot trade Finky_ 🎵",
        "SUFFER, mortals, as your PATHETIC magic BETRAAAAYS YOU!",
        "GET OUT OF DEFILE GET OUT OF DEFILE GET OUT OF DEFILE GET OUT OF DEFILE",
        "So Wooper, how was your week?",
        "I've been kidnapped and forced to create amusing descriptions for raids! Please send help!",
        "If you or any of your loved ones have been affected by any of the issues raised in this raid, please call 0800-RAID-HELP.",
        "If you or any of your loved ones have been affected by the rat-like behaviour of certain rat-like individuals, please call 0800-RAT-HELP.",
        "Shadowmourne is Blood DK prio.",
        "I'm not a real raid description, I'm just a figment of your imagination.",
    };

    public async Task Handle(RaidsSlashCommandExecuted notification, CancellationToken cancellationToken)
    {
        var command = notification.Command;

        await command.RespondAsync(RandomUtils.PickRandom(Acknowledgements), ephemeral: true);

        try
        {
            var clearChannelTasks = _options.Raids.Select(x => ClearChannelAsync(x.ChannelId));
            await Task.WhenAll(clearChannelTasks);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error clearing channels for new raids");
        }

        const DayOfWeek resetDay = DayOfWeek.Wednesday;
        var firstRaidDayOfWeek = _options.Raids.Min(x => x.DayOfWeek.Offset(resetDay));
        var dayOfWeek = DateTime.Now.DayOfWeek.Offset(resetDay);
        var dateOffset = dayOfWeek >= firstRaidDayOfWeek ? 7 - (int)dayOfWeek : 0;

        var createRaidTasks = _options
            .Raids
            .Select(x => CreateRaidAsync(command.User.Id, x.ChannelId, DateTime.Now.NextDayOfWeek(x.DayOfWeek, dateOffset)));

        await Task.WhenAll(createRaidTasks);
    }

    private async Task ClearChannelAsync(ulong channelId)
    {
        var channel = await botContext.Guild.GetChannelAsync(channelId);

        if (channel is not ITextChannel textChannel)
            throw new ApplicationException("Channel was not found or is not a text channel");

        var messages = await textChannel.GetMessagesAsync().FlattenAsync();
        var deletionTasks = messages.Select(x => x.DeleteAsync());

        await Task.WhenAll(deletionTasks);
    }

    private async Task CreateRaidAsync(ulong userId, ulong channelId, DateTime date)
    {
        var request = new CreateEventRequest
        {
            LeaderId = userId.ToString(),
            TemplateId = EventTemplate.WoWClassicWrathOfTheLichKing,
            Date = date.ToShortDateString(),
            Time = "19:30",
            Title = $"ICC 25: {RandomUtils.PickRandom(RaidNames)}",
            Description = RandomUtils.PickRandom(RaidDescriptions),
            AdvancedSettings = new CreateEventRequestAdvancedSettings
            {
                Duration = 210,
                FontStyle = 0,
                TentativeEmote = "remove",
                Mentions = string.Join(',', _options.Mentions)
            }
        };

        await raidHelperClient.CreateEventAsync(botContext.Guild.Id, channelId, request);
    }
}
