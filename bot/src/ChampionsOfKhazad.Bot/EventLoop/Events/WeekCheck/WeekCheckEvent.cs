using Discord;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot.EventLoop;

public class WeekCheckEvent(IOptions<WeekCheckEventOptions> options, BotContext botContext)
    : EventLoopEvent(TimeSpan.FromMinutes(options.Value.MeanTimeToHappenMinutes), "WeekCheck")
{
    private readonly TimeSpan _cooldown = TimeSpan.FromMinutes(options.Value.CooldownMinutes);
    private static DateTimeOffset _lastTriggeredAt = DateTimeOffset.MinValue;

    public override Task<bool> EligibleToFire(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.Now;

        return now - _lastTriggeredAt < _cooldown
            ? Task.FromResult(false)
            : Task.FromResult(now.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday or DayOfWeek.Sunday);
    }

    public override async Task FireAsync(CancellationToken cancellationToken)
    {
        var requestOptions = new RequestOptions { CancelToken = cancellationToken };

        var channel = await botContext.Guild.GetTextChannelAsync(options.Value.TextChannelId, options: requestOptions);

        using var typing = channel.EnterTypingState(requestOptions);

        var message = $"Hey <@{options.Value.UserId}>, how was your week?";

        await channel.SendMessageAsync(message, options: requestOptions);

        _lastTriggeredAt = DateTimeOffset.Now;
    }
}
