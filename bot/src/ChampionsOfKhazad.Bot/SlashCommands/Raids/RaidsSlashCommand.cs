﻿using ChampionsOfKhazad.Bot.RaidHelper;
using Discord;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class RaidsSlashCommand : INotificationHandler<RaidsSlashCommandExecuted>
{
    private readonly RaidsSlashCommandOptions _options;
    private readonly RaidHelperClient _raidHelperClient;
    private readonly BotContext _botContext;
    private readonly ILogger<RaidsSlashCommand> _logger;

    private static readonly string[] Acknowledgements = { "More work?", "Right-o.", "Yes, milord.", "All right.", "Off I go, then!" };

    private static readonly string[] RaidNames =
    {
        "Prepare To Wipe Edition",
        "The Movie: The Game",
        "The Movie: The Game: The Sequel",
        "Leaf's Steak Edition",
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
        "Path Of Frost Edition",
        "Texas Edition",
        "Outshao Edition",
        "7 Drakes",
        "Mumquest",
        "Wooper vs Predator",
        "Frozen Coffee Edition",
        "Live From The Bath!",
        "Hunter's Mark Edition",
    };

    private static readonly string[] RaidDescriptions =
    {
        "Did you guys know you can hero on the first boss and it'll be up in time for the second?",
        "The one where Jaraxxus takes longer than 60 seconds.",
        "Bring your own glizzy.",
        "Yeet.",
        "If you haven't started pinging Cokebeard yet it's already too late.",
        "Followed by pig steak in Hep's cave.",
        "The one where Wooper finally gets pushed too far.",
        "Ask Beaver about his guns.",
        "Don't mass report Yoric again, it's not funny guys, come on.",
        "Tirion Fordring could have just done this himself.",
        "Jk, it's actually Naxx.",
        "Cataclysm waiting room.",
        "ICC waiting room.",
        "The one where Sam and Mards finally kiss.",
        "Remember to wipe once on the Jormungar so Loot Council doesn't have to worry about the cloak.",
        "Hey can we do some achievements?",
        "Stick around for the post-raid zeppelin show!",
        "Damn, Malithas Brightblade got hands.",
        "Come, pig."
    };

    public RaidsSlashCommand(
        IOptions<RaidsSlashCommandOptions> options,
        RaidHelperClient raidHelperClient,
        BotContext botContext,
        ILogger<RaidsSlashCommand> logger
    )
    {
        _options = options.Value;
        _raidHelperClient = raidHelperClient;
        _botContext = botContext;
        _logger = logger;
    }

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
            _logger.LogError(e, "Error clearing channels for new raids");
        }

        const DayOfWeek resetDay = DayOfWeek.Wednesday;
        var firstRaidDayOfWeek = _options.Raids.Min(x => x.DayOfWeek.Offset(resetDay));
        var dayOfWeek = DateTime.Now.DayOfWeek.Offset(resetDay);
        var dateOffset = dayOfWeek >= firstRaidDayOfWeek ? 7 - (int)dayOfWeek : 0;

        var createRaidTasks = _options.Raids.Select(
            x => CreateRaidAsync(command.User.Id, x.ChannelId, DateTime.Now.NextDayOfWeek(x.DayOfWeek, dateOffset))
        );

        await Task.WhenAll(createRaidTasks);
    }

    private async Task ClearChannelAsync(ulong channelId)
    {
        var channel = await _botContext.Guild.GetChannelAsync(channelId);

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
            Title = $"ToGC 25: {RandomUtils.PickRandom(RaidNames)}",
            Description = RandomUtils.PickRandom(RaidDescriptions),
            AdvancedSettings = new CreateEventRequestAdvancedSettings
            {
                Duration = 210,
                FontStyle = 0,
                TentativeEmote = "remove",
                Mentions = string.Join(',', _options.Mentions)
            }
        };

        await _raidHelperClient.CreateEventAsync(_botContext.Guild.Id, channelId, request);
    }
}
