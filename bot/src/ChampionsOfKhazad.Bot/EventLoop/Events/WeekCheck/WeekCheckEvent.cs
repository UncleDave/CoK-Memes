using Discord;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot.EventLoop;

public class WeekCheckEvent(IOptions<WeekCheckEventOptions> options, BotContext botContext)
    : EventLoopEvent(
        TimeSpan.FromMinutes(options.Value.MeanTimeToHappenMinutes),
        "WeekCheck",
        new AllOfEligibilityStrategy(
            new ReasonableHoursEligibilityStrategy(),
            new DayOfWeekEligibilityStrategy(DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday),
            new CooldownEligibilityStrategy("WeekCheck", TimeSpan.FromMinutes(options.Value.CooldownMinutes))
        )
    )
{
    public override async Task FireAsync(CancellationToken cancellationToken)
    {
        var requestOptions = new RequestOptions { CancelToken = cancellationToken };

        var channel = await botContext.Guild.GetTextChannelAsync(options.Value.TextChannelId, options: requestOptions);

        using var typing = channel.EnterTypingState(requestOptions);

        var message = $"Hey <@{options.Value.UserId}>, how was your week?";

        await channel.SendMessageAsync(message, options: requestOptions);

        await base.FireAsync(cancellationToken);
    }
}
