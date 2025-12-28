namespace ChampionsOfKhazad.Bot.EventLoop;

public class ReasonableHoursEligibilityStrategy() : TimeRangeEligibilityStrategy(new TimeOnly(9, 0), new TimeOnly(2, 0));
