﻿using System.Text;

namespace ChampionsOfKhazad.Bot.Lore;

public static class StringBuilderExtensions
{
    public static StringBuilder AppendIf(this StringBuilder sb, bool condition, string value)
    {
        if (condition)
        {
            sb.Append(value);
        }

        return sb;
    }
}
