namespace ChampionsOfKhazad.Bot;

public static class DateExtensions
{
    // An offset of 1 will prevent the current day from being returned.
    public static DateTime NextDayOfWeek(this DateTime start, DayOfWeek day, int offset = 1)
    {
        var result = start.AddDays(offset);

        while (result.DayOfWeek != day)
            result = result.AddDays(1);

        return result;
    }

    public static DayOfWeek Offset(this DayOfWeek dayOfWeek, DayOfWeek startOfWeek)
    {
        var offset = (int)(7 - startOfWeek);
        return (DayOfWeek)(((int)dayOfWeek + offset) % 7);
    }
}
