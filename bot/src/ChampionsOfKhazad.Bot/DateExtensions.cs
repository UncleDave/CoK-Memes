namespace ChampionsOfKhazad.Bot;

public static class DateExtensions
{
    public static DateTime NextDayOfWeek(this DateTime start, DayOfWeek day)
    {
        var result = start.AddDays(1);

        while (result.DayOfWeek != day)
            result = result.AddDays(1);

        return result;
    }
}
